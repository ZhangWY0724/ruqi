using System.Text.Json;
using Microsoft.Data.Sqlite;
using Ruqi.Core.Data;
using Ruqi.Core.Models;

namespace Ruqi.Core.Services;

public sealed class BackupService
{
    public const int CurrentSchemaVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly RuqiDatabase _db;
    private readonly LedgerService _ledger;
    private readonly IClock _clock;

    public BackupService(RuqiDatabase db, LedgerService ledger, IClock? clock = null)
    {
        _db = db;
        _ledger = ledger;
        _clock = clock ?? new SystemClock();
    }

    public async Task<string> ExportBackupJsonAsync(CancellationToken ct = default)
    {
        await _db.InitializeAsync(ct);

        var settings = await _ledger.GetSettingsAsync(ct);
        var nowUtc = _clock.UtcNow;
        settings.LastBackupAt = nowUtc;
        await _ledger.SaveSettingsAsync(settings, ct);

        var channels = await _ledger.GetPaymentChannelsAsync(includeArchived: true, ct);
        var doc = new BackupDocument
        {
            SchemaVersion = CurrentSchemaVersion,
            AppName = "如期",
            ExportedAt = nowUtc,
            Settings = settings,
            CustomCatalogItems = await _ledger.GetCustomCatalogItemsAsync(ct),
            Accounts = await _ledger.GetAccountsAsync(ct: ct),
            CustomPaymentChannels = channels.Where(c => !c.IsBuiltIn).ToList(),
            Subscriptions = await _ledger.GetSubscriptionsAsync(ct),
            Occurrences = await _ledger.GetAllOccurrencesAsync(ct),
            Records = await _ledger.GetAllBillingRecordsAsync(ct)
        };

        return JsonSerializer.Serialize(doc, JsonOptions);
    }

    public BackupPreview ValidateAndPreview(string json)
    {
        var doc = ParseAndValidate(json);
        return new BackupPreview
        {
            SchemaVersion = doc.SchemaVersion,
            ExportedAt = doc.ExportedAt,
            SubscriptionCount = doc.Subscriptions.Count,
            ActiveSubscriptionCount = doc.Subscriptions.Count(s => s.Status == SubscriptionStatus.Active),
            PendingOccurrenceCount = doc.Occurrences.Count(o => o.Kind == OccurrenceKind.Renewal && o.Status == OccurrenceStatus.Pending),
            RecordCount = doc.Records.Count,
            CustomItemCount = doc.CustomCatalogItems.Count + doc.CustomPaymentChannels.Count + doc.Accounts.Count
        };
    }

    public async Task<BackupPreview> RestoreBackupJsonAsync(string json, CancellationToken ct = default)
    {
        await _db.InitializeAsync(ct);
        var doc = ParseAndValidate(json);

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = (SqliteTransaction)await conn.BeginTransactionAsync(ct);

        try
        {
            // 按外键依赖顺序清空
            await using (var clear = conn.CreateCommand())
            {
                clear.Transaction = tx;
                clear.CommandText = """
                    DELETE FROM BillingRecords;
                    DELETE FROM BillingOccurrences;
                    DELETE FROM Subscriptions;
                    DELETE FROM AccountProfiles;
                    DELETE FROM CatalogItems;
                    DELETE FROM PaymentChannels WHERE IsBuiltIn = 0;
                    DELETE FROM AppSettings;
                    """;
                await clear.ExecuteNonQueryAsync(ct);
            }

            // 恢复自定义渠道
            foreach (var ch in doc.CustomPaymentChannels)
            {
                await using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = """
                    INSERT OR IGNORE INTO PaymentChannels (Id, Name, IsBuiltIn, SortOrder, IsArchived)
                    VALUES ($id, $name, 0, $sortOrder, $archived);
                    """;
                cmd.Parameters.AddWithValue("$id", ch.Id);
                cmd.Parameters.AddWithValue("$name", ch.Name.Trim());
                cmd.Parameters.AddWithValue("$sortOrder", ch.SortOrder);
                cmd.Parameters.AddWithValue("$archived", ch.IsArchived ? 1 : 0);
                await cmd.ExecuteNonQueryAsync(ct);
            }

            // 恢复自定义目录
            foreach (var item in doc.CustomCatalogItems)
            {
                await using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = """
                    INSERT INTO CatalogItems
                        (Id, IsBuiltIn, AppName, Category, IconKey, BrandColor, TierName, DefaultCycleType, DefaultInterval, ReferencePriceCents, SearchKeywords)
                    VALUES
                        ($id, 0, $appName, $category, $iconKey, $brandColor, $tierName, $cycleType, $interval, $price, $keywords);
                    """;
                cmd.Parameters.AddWithValue("$id", item.Id);
                cmd.Parameters.AddWithValue("$appName", item.AppName.Trim());
                cmd.Parameters.AddWithValue("$category", string.IsNullOrWhiteSpace(item.Category) ? BuiltInCategories.Other : item.Category.Trim());
                cmd.Parameters.AddWithValue("$iconKey", item.IconKey ?? string.Empty);
                cmd.Parameters.AddWithValue("$brandColor", string.IsNullOrWhiteSpace(item.BrandColor) ? "#5B6CF9" : item.BrandColor);
                cmd.Parameters.AddWithValue("$tierName", (object?)item.TierName ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$cycleType", (int)item.DefaultCycleType);
                cmd.Parameters.AddWithValue("$interval", Math.Max(1, item.DefaultInterval));
                cmd.Parameters.AddWithValue("$price", (object?)item.ReferencePriceCents ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$keywords", item.SearchKeywords ?? string.Empty);
                await cmd.ExecuteNonQueryAsync(ct);
            }

            // 恢复账号
            foreach (var acc in doc.Accounts)
            {
                await using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = """
                    INSERT INTO AccountProfiles (Id, AppName, Nickname, Identifier)
                    VALUES ($id, $appName, $nickname, $identifier);
                    """;
                cmd.Parameters.AddWithValue("$id", acc.Id);
                cmd.Parameters.AddWithValue("$appName", (acc.AppName ?? string.Empty).Trim());
                cmd.Parameters.AddWithValue("$nickname", (object?)acc.Nickname ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$identifier", (object?)acc.Identifier ?? DBNull.Value);
                await cmd.ExecuteNonQueryAsync(ct);
            }

            // 恢复订阅
            foreach (var sub in doc.Subscriptions)
            {
                await LedgerService.UpsertSubscriptionCoreAsync(conn, tx, sub, ct);
            }

            // 恢复期次
            foreach (var occ in doc.Occurrences)
            {
                await LedgerService.InsertOccurrenceIgnoreAsync(conn, tx, occ, ct);
            }

            // 恢复账单
            foreach (var rec in doc.Records)
            {
                await LedgerService.InsertBillingRecordCoreAsync(conn, tx, rec, ct);
            }

            // 恢复设置
            var settings = doc.Settings ?? new AppSettings();
            await LedgerService.UpsertSettingAsync(conn, tx, "DefaultRemindBeforeEnabled", settings.DefaultRemindBeforeEnabled.ToString(), ct);
            await LedgerService.UpsertSettingAsync(conn, tx, "DefaultRemindBeforeDays", Math.Max(1, settings.DefaultRemindBeforeDays).ToString(), ct);
            await LedgerService.UpsertSettingAsync(conn, tx, "DefaultRemindOnDayEnabled", settings.DefaultRemindOnDayEnabled.ToString(), ct);
            await LedgerService.UpsertSettingAsync(conn, tx, "DefaultRemindTime", RuqiDatabase.ToTimeText(settings.DefaultRemindTime), ct);
            if (settings.LastBackupAt.HasValue)
            {
                await LedgerService.UpsertSettingAsync(conn, tx, "LastBackupAt", RuqiDatabase.ToIsoTimestamp(settings.LastBackupAt.Value), ct);
            }

            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }

        await _ledger.SyncDueOccurrencesAsync(ct: ct);
        return ValidateAndPreview(json);
    }

    private static BackupDocument ParseAndValidate(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidDataException("备份文件内容为空。");
        }

        BackupDocument? doc;
        try
        {
            doc = JsonSerializer.Deserialize<BackupDocument>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"备份文件 JSON 格式损坏：{ex.Message}");
        }

        if (doc is null)
        {
            throw new InvalidDataException("无法解析备份文件。");
        }

        if (doc.SchemaVersion <= 0 || doc.SchemaVersion > CurrentSchemaVersion)
        {
            throw new InvalidDataException($"不支持的备份版本号（v{doc.SchemaVersion}），当前支持最高 v{CurrentSchemaVersion}。");
        }

        doc.CustomCatalogItems ??= [];
        doc.Accounts ??= [];
        doc.CustomPaymentChannels ??= [];
        doc.Subscriptions ??= [];
        doc.Occurrences ??= [];
        doc.Records ??= [];

        var subIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var sub in doc.Subscriptions)
        {
            if (string.IsNullOrWhiteSpace(sub.Id) || string.IsNullOrWhiteSpace(sub.AppName))
            {
                throw new InvalidDataException("备份中的订阅记录缺少必要字段（Id 或应用名称）。");
            }

            if (sub.AmountCents < 0)
            {
                throw new InvalidDataException($"订阅「{sub.AppName}」金额非法。");
            }

            subIds.Add(sub.Id);
        }

        foreach (var occ in doc.Occurrences)
        {
            if (string.IsNullOrWhiteSpace(occ.Id) || !subIds.Contains(occ.SubscriptionId))
            {
                throw new InvalidDataException("备份中的期次记录引用了不存在的订阅。");
            }
        }

        foreach (var rec in doc.Records)
        {
            if (string.IsNullOrWhiteSpace(rec.Id) || string.IsNullOrWhiteSpace(rec.AppNameSnapshot) || rec.PaidAmountCents < 0)
            {
                throw new InvalidDataException("备份中的扣款记录存在损坏字段。");
            }

            if (!string.IsNullOrWhiteSpace(rec.SubscriptionId) && !subIds.Contains(rec.SubscriptionId))
            {
                rec.SubscriptionId = null;
            }
        }

        return doc;
    }
}
