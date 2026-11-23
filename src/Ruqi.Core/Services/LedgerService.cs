using Microsoft.Data.Sqlite;
using Ruqi.Core.Data;
using Ruqi.Core.Models;

namespace Ruqi.Core.Services;

public sealed class LedgerService
{
    private readonly RuqiDatabase _db;
    private readonly IClock _clock;

    public LedgerService(RuqiDatabase db, IClock? clock = null)
    {
        _db = db;
        _clock = clock ?? new SystemClock();
    }

    public Task InitializeAsync(CancellationToken ct = default) => _db.InitializeAsync(ct);

    #region Catalog / Account / PaymentChannel

    public async Task<List<CatalogItem>> SearchCatalogAsync(string? keyword = null, string? category = null, CancellationToken ct = default)
    {
        await _db.InitializeAsync(ct);
        var builtIn = BuiltInSeeds.GetBuiltInCatalogItems();
        var custom = await GetCustomCatalogItemsAsync(ct);

        var all = new List<CatalogItem>(custom.Count + builtIn.Count);
        all.AddRange(custom);
        all.AddRange(builtIn);

        if (!string.IsNullOrWhiteSpace(category) && category != "全部")
        {
            all = all.Where(x => string.Equals(x.Category, category, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var q = keyword.Trim();
            all = all.Where(x =>
                x.AppName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrEmpty(x.TierName) && x.TierName.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                x.Category.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                x.SearchKeywords.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        return all;
    }

    public async Task<List<CatalogItem>> GetCustomCatalogItemsAsync(CancellationToken ct = default)
    {
        await _db.InitializeAsync(ct);
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM CatalogItems WHERE IsBuiltIn = 0 ORDER BY AppName, TierName;";
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var list = new List<CatalogItem>();
        while (await reader.ReadAsync(ct))
        {
            list.Add(ReadCatalogItem(reader));
        }

        return list;
    }

    public async Task<CatalogItem> SaveCustomCatalogItemAsync(CatalogItem item, CancellationToken ct = default)
    {
        await _db.InitializeAsync(ct);
        var appName = item.AppName?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(appName))
        {
            throw new ArgumentException("应用名称不能为空。");
        }

        item.Id = string.IsNullOrWhiteSpace(item.Id) ? Guid.NewGuid().ToString("N") : item.Id;
        item.IsBuiltIn = false;
        item.AppName = appName;
        item.Category = string.IsNullOrWhiteSpace(item.Category) ? BuiltInCategories.Other : item.Category.Trim();
        item.TierName = string.IsNullOrWhiteSpace(item.TierName) ? null : item.TierName.Trim();
        item.BrandColor = string.IsNullOrWhiteSpace(item.BrandColor) ? FormatHelpers.PickFallbackColor(appName) : item.BrandColor.Trim();
        item.DefaultInterval = Math.Max(1, item.DefaultInterval);

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO CatalogItems
                (Id, IsBuiltIn, AppName, Category, IconKey, BrandColor, TierName, DefaultCycleType, DefaultInterval, ReferencePriceCents, SearchKeywords)
            VALUES
                ($id, 0, $appName, $category, $iconKey, $brandColor, $tierName, $cycleType, $interval, $price, $keywords)
            ON CONFLICT(Id) DO UPDATE SET
                AppName = excluded.AppName,
                Category = excluded.Category,
                IconKey = excluded.IconKey,
                BrandColor = excluded.BrandColor,
                TierName = excluded.TierName,
                DefaultCycleType = excluded.DefaultCycleType,
                DefaultInterval = excluded.DefaultInterval,
                ReferencePriceCents = excluded.ReferencePriceCents,
                SearchKeywords = excluded.SearchKeywords;
            """;
        cmd.Parameters.AddWithValue("$id", item.Id);
        cmd.Parameters.AddWithValue("$appName", item.AppName);
        cmd.Parameters.AddWithValue("$category", item.Category);
        cmd.Parameters.AddWithValue("$iconKey", item.IconKey ?? string.Empty);
        cmd.Parameters.AddWithValue("$brandColor", item.BrandColor);
        cmd.Parameters.AddWithValue("$tierName", (object?)item.TierName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$cycleType", (int)item.DefaultCycleType);
        cmd.Parameters.AddWithValue("$interval", item.DefaultInterval);
        cmd.Parameters.AddWithValue("$price", (object?)item.ReferencePriceCents ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$keywords", item.SearchKeywords ?? string.Empty);
        await cmd.ExecuteNonQueryAsync(ct);

        return item;
    }

    public async Task DeleteCustomCatalogItemAsync(string id, CancellationToken ct = default)
    {
        await _db.InitializeAsync(ct);
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM CatalogItems WHERE Id = $id AND IsBuiltIn = 0;";
        cmd.Parameters.AddWithValue("$id", id);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<AccountProfile>> GetAccountsAsync(string? appName = null, CancellationToken ct = default)
    {
        await _db.InitializeAsync(ct);
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        // 常用账号为全局跨应用共享池（手机号/邮箱 + 可选简称），不再按应用隔离
        cmd.CommandText = "SELECT * FROM AccountProfiles ORDER BY COALESCE(Nickname, Identifier), Identifier;";

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var list = new List<AccountProfile>();
        while (await reader.ReadAsync(ct))
        {
            list.Add(ReadAccountProfile(reader));
        }

        return list;
    }

    public async Task<AccountProfile> SaveAccountAsync(AccountProfile profile, CancellationToken ct = default)
    {
        await _db.InitializeAsync(ct);
        var nickname = string.IsNullOrWhiteSpace(profile.Nickname) ? null : profile.Nickname.Trim();
        var identifier = string.IsNullOrWhiteSpace(profile.Identifier) ? null : profile.Identifier.Trim();

        if (nickname is null && identifier is null)
        {
            throw new ArgumentException("请至少填写手机号/邮箱或账号简称。");
        }

        var isNew = string.IsNullOrWhiteSpace(profile.Id);
        if (isNew && identifier is not null)
        {
            // 全局按手机号/邮箱去重复用
            var all = await GetAccountsAsync(ct: ct);
            var dup = all.FirstOrDefault(a => string.Equals(a.Identifier, identifier, StringComparison.OrdinalIgnoreCase));
            if (dup is not null)
            {
                profile.Id = dup.Id;
                nickname ??= dup.Nickname;
            }
        }

        profile.Id = string.IsNullOrWhiteSpace(profile.Id) ? Guid.NewGuid().ToString("N") : profile.Id;
        profile.AppName = string.Empty;
        profile.Nickname = nickname;
        profile.Identifier = identifier;

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = (SqliteTransaction)await conn.BeginTransactionAsync(ct);

        await using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO AccountProfiles (Id, AppName, Nickname, Identifier)
                VALUES ($id, $appName, $nickname, $identifier)
                ON CONFLICT(Id) DO UPDATE SET
                    AppName = excluded.AppName,
                    Nickname = excluded.Nickname,
                    Identifier = excluded.Identifier;
                """;
            cmd.Parameters.AddWithValue("$id", profile.Id);
            cmd.Parameters.AddWithValue("$appName", profile.AppName);
            cmd.Parameters.AddWithValue("$nickname", (object?)profile.Nickname ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$identifier", (object?)profile.Identifier ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        // 同步更新关联订阅的展示缓存
        var display = FormatHelpers.BuildAccountDisplay(profile.Nickname, profile.Identifier);
        await using (var syncCmd = conn.CreateCommand())
        {
            syncCmd.Transaction = tx;
            syncCmd.CommandText = "UPDATE Subscriptions SET AccountDisplayCache = $display WHERE AccountId = $id;";
            syncCmd.Parameters.AddWithValue("$display", (object?)display ?? DBNull.Value);
            syncCmd.Parameters.AddWithValue("$id", profile.Id);
            await syncCmd.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
        return profile;
    }

    public async Task DeleteAccountAsync(string id, CancellationToken ct = default)
    {
        await _db.InitializeAsync(ct);
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = (SqliteTransaction)await conn.BeginTransactionAsync(ct);

        // 保留已有订阅的展示快照，仅断开关联 ID
        await using (var unlink = conn.CreateCommand())
        {
            unlink.Transaction = tx;
            unlink.CommandText = "UPDATE Subscriptions SET AccountId = NULL WHERE AccountId = $id;";
            unlink.Parameters.AddWithValue("$id", id);
            await unlink.ExecuteNonQueryAsync(ct);
        }

        await using (var del = conn.CreateCommand())
        {
            del.Transaction = tx;
            del.CommandText = "DELETE FROM AccountProfiles WHERE Id = $id;";
            del.Parameters.AddWithValue("$id", id);
            await del.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
    }

    public async Task<List<PaymentChannel>> GetPaymentChannelsAsync(bool includeArchived = false, CancellationToken ct = default)
    {
        await _db.InitializeAsync(ct);
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = includeArchived
            ? "SELECT * FROM PaymentChannels ORDER BY SortOrder, Name;"
            : "SELECT * FROM PaymentChannels WHERE IsArchived = 0 ORDER BY SortOrder, Name;";
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var list = new List<PaymentChannel>();
        while (await reader.ReadAsync(ct))
        {
            list.Add(ReadPaymentChannel(reader));
        }

        return list;
    }

    public async Task<PaymentChannel> AddCustomPaymentChannelAsync(string name, CancellationToken ct = default)
    {
        await _db.InitializeAsync(ct);
        var trimmed = name?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new ArgumentException("付款渠道名称不能为空。");
        }

        await using var conn = await _db.OpenConnectionAsync(ct);

        await using (var exists = conn.CreateCommand())
        {
            exists.CommandText = "SELECT * FROM PaymentChannels WHERE lower(Name) = lower($name) LIMIT 1;";
            exists.Parameters.AddWithValue("$name", trimmed);
            await using var reader = await exists.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                var found = ReadPaymentChannel(reader);
                if (found.IsArchived)
                {
                    await ToggleArchivePaymentChannelAsync(found.Id, false, ct);
                    found.IsArchived = false;
                }
                return found;
            }
        }

        var channel = new PaymentChannel
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = trimmed,
            IsBuiltIn = false,
            SortOrder = 50,
            IsArchived = false
        };

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO PaymentChannels (Id, Name, IsBuiltIn, SortOrder, IsArchived)
            VALUES ($id, $name, 0, $sortOrder, 0);
            """;
        cmd.Parameters.AddWithValue("$id", channel.Id);
        cmd.Parameters.AddWithValue("$name", channel.Name);
        cmd.Parameters.AddWithValue("$sortOrder", channel.SortOrder);
        await cmd.ExecuteNonQueryAsync(ct);
        return channel;
    }

    public async Task RenamePaymentChannelAsync(string id, string newName, CancellationToken ct = default)
    {
        await _db.InitializeAsync(ct);
        var trimmed = newName?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new ArgumentException("渠道名称不能为空。");
        }

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE PaymentChannels SET Name = $name WHERE Id = $id AND IsBuiltIn = 0;";
        cmd.Parameters.AddWithValue("$name", trimmed);
        cmd.Parameters.AddWithValue("$id", id);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task ToggleArchivePaymentChannelAsync(string id, bool archived, CancellationToken ct = default)
    {
        await _db.InitializeAsync(ct);
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE PaymentChannels SET IsArchived = $archived WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$archived", archived ? 1 : 0);
        cmd.Parameters.AddWithValue("$id", id);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteCustomPaymentChannelAsync(string id, CancellationToken ct = default)
    {
        await _db.InitializeAsync(ct);
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM PaymentChannels WHERE Id = $id AND IsBuiltIn = 0;";
        cmd.Parameters.AddWithValue("$id", id);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    #endregion

    #region Subscriptions

    public async Task<List<Subscription>> GetSubscriptionsAsync(CancellationToken ct = default)
    {
        await _db.InitializeAsync(ct);
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Subscriptions ORDER BY Status ASC, NextBillingDate ASC, AppName ASC;";
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var list = new List<Subscription>();
        while (await reader.ReadAsync(ct))
        {
            list.Add(ReadSubscription(reader));
        }

        return list;
    }

    public async Task<Subscription?> GetSubscriptionByIdAsync(string id, CancellationToken ct = default)
    {
        await _db.InitializeAsync(ct);
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Subscriptions WHERE Id = $id LIMIT 1;";
        cmd.Parameters.AddWithValue("$id", id);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? ReadSubscription(reader) : null;
    }

    public async Task<SubscriptionDraft> CreateCopyDraftAsync(string sourceSubscriptionId, CancellationToken ct = default)
    {
        var source = await GetSubscriptionByIdAsync(sourceSubscriptionId, ct)
            ?? throw new InvalidOperationException("未找到原订阅。");

        return new SubscriptionDraft
        {
            Id = null,
            AppName = source.AppName,
            Category = source.Category,
            IconKey = source.IconKey,
            BrandColor = source.BrandColor,
            TierName = source.TierName,
            AmountCents = source.AmountCents,
            CycleType = source.CycleType,
            CycleInterval = source.CycleInterval,
            NextBillingDate = _clock.Today,
            AccountId = null,
            AccountNickname = null,
            AccountIdentifier = null,
            PaymentChannelName = source.PaymentChannelName,
            RemindBeforeEnabled = source.RemindBeforeEnabled,
            RemindBeforeDays = source.RemindBeforeDays,
            RemindOnDayEnabled = source.RemindOnDayEnabled,
            RemindTime = source.RemindTime,
            Note = source.Note
        };
    }

    public async Task<Subscription> SaveSubscriptionAsync(SubscriptionDraft draft, bool enforceTodayOrFuture = true, CancellationToken ct = default)
    {
        await _db.InitializeAsync(ct);

        var appName = draft.AppName?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(appName))
        {
            throw new ArgumentException("请输入应用名称。");
        }

        if (draft.AmountCents < 0)
        {
            throw new ArgumentException("金额不能为负数。");
        }

        if (draft.CycleType is BillingCycleType.EveryNDays or BillingCycleType.EveryNMonths && draft.CycleInterval < 1)
        {
            throw new ArgumentException("自定义周期间隔必须为正整数。");
        }

        var today = _clock.Today;
        var isEdit = !string.IsNullOrWhiteSpace(draft.Id);
        Subscription? existing = isEdit ? await GetSubscriptionByIdAsync(draft.Id!, ct) : null;
        if (isEdit && existing is null)
        {
            throw new InvalidOperationException("未找到要编辑的订阅。");
        }

        var dateChanged = existing is null || existing.NextBillingDate != draft.NextBillingDate;
        if (enforceTodayOrFuture && dateChanged && draft.NextBillingDate < today)
        {
            throw new ArgumentException("下次扣费日或到期日不能早于今天。如需补记历史账单，请保存后在详情页点击「+ 补录账单」。");
        }

        if (draft.RemindBeforeEnabled && draft.RemindBeforeDays < 1)
        {
            throw new ArgumentException("提前提醒天数必须为正整数。");
        }

        // 解析/创建关联账号
        string? accountId = draft.AccountId;
        string? accountDisplay = null;
        if (!string.IsNullOrWhiteSpace(accountId))
        {
            var accounts = await GetAccountsAsync(ct: ct);
            var acc = accounts.FirstOrDefault(x => x.Id == accountId);
            if (acc is not null)
            {
                accountDisplay = FormatHelpers.BuildAccountDisplay(acc.Nickname, acc.Identifier);
            }
            else
            {
                accountId = null;
            }
        }
        else if (!string.IsNullOrWhiteSpace(draft.AccountNickname) || !string.IsNullOrWhiteSpace(draft.AccountIdentifier))
        {
            var existingAccounts = await GetAccountsAsync(ct: ct);
            var nickTrim = string.IsNullOrWhiteSpace(draft.AccountNickname) ? null : draft.AccountNickname.Trim();
            var idTrim = string.IsNullOrWhiteSpace(draft.AccountIdentifier) ? null : draft.AccountIdentifier.Trim();

            var matched = idTrim is not null
                ? existingAccounts.FirstOrDefault(a => string.Equals(a.Identifier, idTrim, StringComparison.OrdinalIgnoreCase))
                : existingAccounts.FirstOrDefault(a => string.Equals(a.Nickname, nickTrim, StringComparison.OrdinalIgnoreCase) && a.Identifier is null);

            if (matched is not null)
            {
                accountId = matched.Id;
                accountDisplay = FormatHelpers.BuildAccountDisplay(nickTrim ?? matched.Nickname, matched.Identifier);
            }
            else
            {
                var createdAcc = await SaveAccountAsync(new AccountProfile
                {
                    AppName = string.Empty,
                    Nickname = nickTrim,
                    Identifier = idTrim
                }, ct);
                accountId = createdAcc.Id;
                accountDisplay = FormatHelpers.BuildAccountDisplay(createdAcc.Nickname, createdAcc.Identifier);
            }
        }

        var category = string.IsNullOrWhiteSpace(draft.Category) ? BuiltInCategories.Other : draft.Category.Trim();
        var tierName = string.IsNullOrWhiteSpace(draft.TierName) ? null : draft.TierName.Trim();
        var brandColor = string.IsNullOrWhiteSpace(draft.BrandColor) ? FormatHelpers.PickFallbackColor(appName) : draft.BrandColor.Trim();
        var paymentChannel = string.IsNullOrWhiteSpace(draft.PaymentChannelName) ? null : draft.PaymentChannelName.Trim();
        var note = string.IsNullOrWhiteSpace(draft.Note) ? null : draft.Note.Trim();
        var nowUtc = _clock.UtcNow;

        var sub = existing ?? new Subscription { Id = Guid.NewGuid().ToString("N"), CreatedAt = nowUtc };
        sub.AppName = appName;
        sub.Category = category;
        sub.IconKey = draft.IconKey ?? string.Empty;
        sub.BrandColor = brandColor;
        sub.TierName = tierName;
        sub.AmountCents = draft.AmountCents;
        sub.Currency = "CNY";
        sub.CycleType = draft.CycleType;
        sub.CycleInterval = draft.CycleType is BillingCycleType.EveryNDays or BillingCycleType.EveryNMonths
            ? Math.Max(1, draft.CycleInterval)
            : 1;

        if (existing is null || dateChanged)
        {
            sub.NextBillingDate = draft.NextBillingDate;
            sub.AnchorDay = draft.NextBillingDate.Day;
            sub.AnchorMonth = draft.NextBillingDate.Month;
        }

        if (sub.CycleType == BillingCycleType.OneTime)
        {
            sub.ExpiryDate = sub.NextBillingDate;
        }
        else if (existing is not null && existing.CycleType == BillingCycleType.OneTime && sub.Status == SubscriptionStatus.Active)
        {
            sub.ExpiryDate = null;
        }

        sub.AccountId = accountId;
        sub.AccountDisplayCache = accountDisplay;
        sub.PaymentChannelName = paymentChannel;
        sub.RemindBeforeEnabled = draft.RemindBeforeEnabled;
        sub.RemindBeforeDays = Math.Max(1, draft.RemindBeforeDays);
        sub.RemindOnDayEnabled = draft.RemindOnDayEnabled;
        sub.RemindTime = draft.RemindTime;
        sub.Note = note;
        sub.UpdatedAt = nowUtc;

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = (SqliteTransaction)await conn.BeginTransactionAsync(ct);

        await UpsertSubscriptionCoreAsync(conn, tx, sub, ct);

        if (sub.CycleType == BillingCycleType.OneTime)
        {
            // 清理旧的仅到期记录并写入当前到期日
            await using (var delExp = conn.CreateCommand())
            {
                delExp.Transaction = tx;
                delExp.CommandText = "DELETE FROM BillingOccurrences WHERE SubscriptionId = $subId AND Kind = $kind;";
                delExp.Parameters.AddWithValue("$subId", sub.Id);
                delExp.Parameters.AddWithValue("$kind", (int)OccurrenceKind.ExpiryOnly);
                await delExp.ExecuteNonQueryAsync(ct);
            }

            await InsertOccurrenceIgnoreAsync(conn, tx, new BillingOccurrence
            {
                SubscriptionId = sub.Id,
                ScheduledDate = sub.NextBillingDate,
                ExpectedAmountCents = sub.AmountCents,
                Kind = OccurrenceKind.ExpiryOnly,
                Status = OccurrenceStatus.Planned,
                CreatedAt = nowUtc,
                UpdatedAt = nowUtc
            }, ct);

            // 新增一次性项目时可选：同时记入一笔已付账单
            if (!isEdit && draft.RecordOneTimePayment)
            {
                var paidCents = draft.OneTimePaidAmountCents ?? sub.AmountCents;
                if (paidCents < 0)
                {
                    throw new ArgumentException("实付金额不能为负数。");
                }

                var paidDate = draft.OneTimePaidDate ?? today;
                await InsertBillingRecordCoreAsync(conn, tx, new BillingRecord
                {
                    OccurrenceId = null,
                    SubscriptionId = sub.Id,
                    PaidAmountCents = paidCents,
                    Currency = "CNY",
                    PaidDate = paidDate,
                    AppNameSnapshot = sub.AppName,
                    CategorySnapshot = sub.Category,
                    IconKeySnapshot = sub.IconKey,
                    BrandColorSnapshot = sub.BrandColor,
                    TierNameSnapshot = sub.TierName,
                    AccountDisplaySnapshot = sub.AccountDisplayCache,
                    PaymentChannelSnapshot = sub.PaymentChannelName,
                    Note = sub.Note,
                    CreatedAt = nowUtc
                }, ct);
            }
        }
        else if (isEdit && dateChanged)
        {
            // 修改下次扣费日后清理尚未到期的 Planned 期次，保留 Pending / Confirmed / Skipped
            await using var delPlanned = conn.CreateCommand();
            delPlanned.Transaction = tx;
            delPlanned.CommandText = "DELETE FROM BillingOccurrences WHERE SubscriptionId = $subId AND Status = $planned;";
            delPlanned.Parameters.AddWithValue("$subId", sub.Id);
            delPlanned.Parameters.AddWithValue("$planned", (int)OccurrenceStatus.Planned);
            await delPlanned.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);

        // 若目录中尚无此应用+档位，自动沉淀为自定义目录项供复用
        await EnsureCustomCatalogIfMissingAsync(sub, ct);

        // 若下次扣费日落在今天（或之前，如测试场景），立即补齐期次
        await SyncDueOccurrencesAsync(today, ct);

        return (await GetSubscriptionByIdAsync(sub.Id, ct)) ?? sub;
    }

    public async Task MarkAsCancelledAsync(string subscriptionId, DateOnly? expiryDate = null, CancellationToken ct = default)
    {
        await _db.InitializeAsync(ct);
        var sub = await GetSubscriptionByIdAsync(subscriptionId, ct)
            ?? throw new InvalidOperationException("未找到该订阅。");

        var nowUtc = _clock.UtcNow;
        sub.Status = SubscriptionStatus.Cancelled;
        sub.ExpiryDate = expiryDate;
        sub.UpdatedAt = nowUtc;

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = (SqliteTransaction)await conn.BeginTransactionAsync(ct);

        await UpsertSubscriptionCoreAsync(conn, tx, sub, ct);

        // 移除尚未到期的 Planned 续费计划与旧的 ExpiryOnly，保留历史与已到期 Pending
        await using (var delCmd = conn.CreateCommand())
        {
            delCmd.Transaction = tx;
            delCmd.CommandText = """
                DELETE FROM BillingOccurrences
                WHERE SubscriptionId = $subId
                  AND (Status = $planned OR Kind = $expiryKind);
                """;
            delCmd.Parameters.AddWithValue("$subId", sub.Id);
            delCmd.Parameters.AddWithValue("$planned", (int)OccurrenceStatus.Planned);
            delCmd.Parameters.AddWithValue("$expiryKind", (int)OccurrenceKind.ExpiryOnly);
            await delCmd.ExecuteNonQueryAsync(ct);
        }

        if (expiryDate.HasValue)
        {
            await InsertOccurrenceIgnoreAsync(conn, tx, new BillingOccurrence
            {
                SubscriptionId = sub.Id,
                ScheduledDate = expiryDate.Value,
                ExpectedAmountCents = 0,
                Kind = OccurrenceKind.ExpiryOnly,
                Status = OccurrenceStatus.Planned,
                CreatedAt = nowUtc,
                UpdatedAt = nowUtc
            }, ct);
        }

        await tx.CommitAsync(ct);
    }

    public async Task<Subscription> ResumeSubscriptionAsync(
        string subscriptionId,
        DateOnly nextBillingDate,
        long? newAmountCents = null,
        CancellationToken ct = default)
    {
        await _db.InitializeAsync(ct);
        var today = _clock.Today;
        if (nextBillingDate < today)
        {
            throw new ArgumentException("恢复续费的下次扣费日不能早于今天。");
        }

        var sub = await GetSubscriptionByIdAsync(subscriptionId, ct)
            ?? throw new InvalidOperationException("未找到该订阅。");

        if (newAmountCents.HasValue && newAmountCents.Value < 0)
        {
            throw new ArgumentException("金额不能为负数。");
        }

        var nowUtc = _clock.UtcNow;
        sub.Status = SubscriptionStatus.Active;
        if (newAmountCents.HasValue)
        {
            sub.AmountCents = newAmountCents.Value;
        }

        sub.NextBillingDate = nextBillingDate;
        sub.AnchorDay = nextBillingDate.Day;
        sub.AnchorMonth = nextBillingDate.Month;
        sub.ExpiryDate = sub.CycleType == BillingCycleType.OneTime ? nextBillingDate : null;
        sub.UpdatedAt = nowUtc;

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = (SqliteTransaction)await conn.BeginTransactionAsync(ct);

        await UpsertSubscriptionCoreAsync(conn, tx, sub, ct);

        await using (var delExp = conn.CreateCommand())
        {
            delExp.Transaction = tx;
            delExp.CommandText = "DELETE FROM BillingOccurrences WHERE SubscriptionId = $subId AND Kind = $kind;";
            delExp.Parameters.AddWithValue("$subId", sub.Id);
            delExp.Parameters.AddWithValue("$kind", (int)OccurrenceKind.ExpiryOnly);
            await delExp.ExecuteNonQueryAsync(ct);
        }

        if (sub.CycleType == BillingCycleType.OneTime)
        {
            await InsertOccurrenceIgnoreAsync(conn, tx, new BillingOccurrence
            {
                SubscriptionId = sub.Id,
                ScheduledDate = nextBillingDate,
                ExpectedAmountCents = sub.AmountCents,
                Kind = OccurrenceKind.ExpiryOnly,
                Status = OccurrenceStatus.Planned,
                CreatedAt = nowUtc,
                UpdatedAt = nowUtc
            }, ct);
        }

        await tx.CommitAsync(ct);

        await SyncDueOccurrencesAsync(today, ct);
        return (await GetSubscriptionByIdAsync(sub.Id, ct)) ?? sub;
    }

    public async Task DeleteSubscriptionAsync(string subscriptionId, DeleteSubscriptionMode mode, CancellationToken ct = default)
    {
        await _db.InitializeAsync(ct);
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = (SqliteTransaction)await conn.BeginTransactionAsync(ct);

        if (mode == DeleteSubscriptionMode.DeleteHistory)
        {
            await using var delRec = conn.CreateCommand();
            delRec.Transaction = tx;
            delRec.CommandText = "DELETE FROM BillingRecords WHERE SubscriptionId = $subId;";
            delRec.Parameters.AddWithValue("$subId", subscriptionId);
            await delRec.ExecuteNonQueryAsync(ct);
        }
        else
        {
            // KeepHistory：显式断开 SubscriptionId，保留快照字段
            await using var detachRec = conn.CreateCommand();
            detachRec.Transaction = tx;
            detachRec.CommandText = "UPDATE BillingRecords SET SubscriptionId = NULL WHERE SubscriptionId = $subId;";
            detachRec.Parameters.AddWithValue("$subId", subscriptionId);
            await detachRec.ExecuteNonQueryAsync(ct);
        }

        // 外键级联删除 BillingOccurrences
        await using (var delSub = conn.CreateCommand())
        {
            delSub.Transaction = tx;
            delSub.CommandText = "DELETE FROM Subscriptions WHERE Id = $subId;";
            delSub.Parameters.AddWithValue("$subId", subscriptionId);
            await delSub.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
    }

    #endregion

    #region Occurrences & Billing Records

    public async Task<int> SyncDueOccurrencesAsync(DateOnly? asOfDate = null, CancellationToken ct = default)
    {
        await _db.InitializeAsync(ct);
        var today = asOfDate ?? _clock.Today;
        var nowUtc = _clock.UtcNow;
        var createdCount = 0;

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = (SqliteTransaction)await conn.BeginTransactionAsync(ct);

        var activeSubs = new List<Subscription>();
        await using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "SELECT * FROM Subscriptions WHERE Status = 0 AND CycleType != $oneTime;";
            cmd.Parameters.AddWithValue("$oneTime", (int)BillingCycleType.OneTime);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                activeSubs.Add(ReadSubscription(reader));
            }
        }

        foreach (var sub in activeSubs)
        {
            var cursor = sub.NextBillingDate;
            var changed = false;
            var guard = 0;

            while (cursor <= today && guard < 1000)
            {
                var inserted = await EnsurePendingOccurrenceAsync(conn, tx, sub, cursor, nowUtc, ct);
                if (inserted)
                {
                    createdCount++;
                }

                var next = CycleCalculator.GetNextDate(
                    cursor,
                    sub.CycleType,
                    sub.CycleInterval,
                    sub.AnchorDay,
                    sub.AnchorMonth);

                if (next <= cursor)
                {
                    break;
                }

                cursor = next;
                changed = true;
                guard++;
            }

            if (changed)
            {
                await using var upd = conn.CreateCommand();
                upd.Transaction = tx;
                upd.CommandText = "UPDATE Subscriptions SET NextBillingDate = $nextDate, UpdatedAt = $updatedAt WHERE Id = $id;";
                upd.Parameters.AddWithValue("$nextDate", RuqiDatabase.ToIsoDate(cursor));
                upd.Parameters.AddWithValue("$updatedAt", RuqiDatabase.ToIsoTimestamp(nowUtc));
                upd.Parameters.AddWithValue("$id", sub.Id);
                await upd.ExecuteNonQueryAsync(ct);
            }
        }

        await tx.CommitAsync(ct);
        return createdCount;
    }

    public async Task<BillingOccurrence?> GetOccurrenceByIdAsync(string occurrenceId, CancellationToken ct = default)
    {
        await _db.InitializeAsync(ct);
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM BillingOccurrences WHERE Id = $id LIMIT 1;";
        cmd.Parameters.AddWithValue("$id", occurrenceId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? ReadOccurrence(reader) : null;
    }

    public async Task<List<BillingOccurrence>> GetPendingOccurrencesBySubscriptionAsync(string subscriptionId, CancellationToken ct = default)
    {
        await _db.InitializeAsync(ct);
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT * FROM BillingOccurrences
            WHERE SubscriptionId = $subId AND Kind = $renewal AND Status = $pending
            ORDER BY ScheduledDate ASC;
            """;
        cmd.Parameters.AddWithValue("$subId", subscriptionId);
        cmd.Parameters.AddWithValue("$renewal", (int)OccurrenceKind.Renewal);
        cmd.Parameters.AddWithValue("$pending", (int)OccurrenceStatus.Pending);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var list = new List<BillingOccurrence>();
        while (await reader.ReadAsync(ct))
        {
            list.Add(ReadOccurrence(reader));
        }

        return list;
    }

    public async Task<BillingRecord> ConfirmOccurrenceAsync(
        string occurrenceId,
        long paidAmountCents,
        DateOnly paidDate,
        string? note = null,
        CancellationToken ct = default)
    {
        await _db.InitializeAsync(ct);
        if (paidAmountCents < 0)
        {
            throw new ArgumentException("实付金额不能为负数。");
        }

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = (SqliteTransaction)await conn.BeginTransactionAsync(ct);

        BillingOccurrence? occ = null;
        await using (var qOcc = conn.CreateCommand())
        {
            qOcc.Transaction = tx;
            qOcc.CommandText = "SELECT * FROM BillingOccurrences WHERE Id = $id LIMIT 1;";
            qOcc.Parameters.AddWithValue("$id", occurrenceId);
            await using var reader = await qOcc.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                occ = ReadOccurrence(reader);
            }
        }

        if (occ is null)
        {
            throw new InvalidOperationException("未找到该扣款期次。");
        }

        // 幂等检查：若已存在关联账单直接返回，绝不重复记账
        await using (var qRec = conn.CreateCommand())
        {
            qRec.Transaction = tx;
            qRec.CommandText = "SELECT * FROM BillingRecords WHERE OccurrenceId = $occId LIMIT 1;";
            qRec.Parameters.AddWithValue("$occId", occurrenceId);
            await using var reader = await qRec.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                var existingRec = ReadBillingRecord(reader);
                await tx.CommitAsync(ct);
                return existingRec;
            }
        }

        if (occ.Status == OccurrenceStatus.Skipped)
        {
            throw new InvalidOperationException("该期次已标记为本期未扣款。");
        }

        Subscription? sub = null;
        await using (var qSub = conn.CreateCommand())
        {
            qSub.Transaction = tx;
            qSub.CommandText = "SELECT * FROM Subscriptions WHERE Id = $id LIMIT 1;";
            qSub.Parameters.AddWithValue("$id", occ.SubscriptionId);
            await using var reader = await qSub.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                sub = ReadSubscription(reader);
            }
        }

        if (sub is null)
        {
            throw new InvalidOperationException("关联的订阅不存在。");
        }

        var nowUtc = _clock.UtcNow;

        await using (var updOcc = conn.CreateCommand())
        {
            updOcc.Transaction = tx;
            updOcc.CommandText = "UPDATE BillingOccurrences SET Status = $confirmed, UpdatedAt = $updatedAt WHERE Id = $id;";
            updOcc.Parameters.AddWithValue("$confirmed", (int)OccurrenceStatus.Confirmed);
            updOcc.Parameters.AddWithValue("$updatedAt", RuqiDatabase.ToIsoTimestamp(nowUtc));
            updOcc.Parameters.AddWithValue("$id", occ.Id);
            await updOcc.ExecuteNonQueryAsync(ct);
        }

        var record = new BillingRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            OccurrenceId = occ.Id,
            SubscriptionId = sub.Id,
            PaidAmountCents = paidAmountCents,
            Currency = "CNY",
            PaidDate = paidDate,
            AppNameSnapshot = sub.AppName,
            CategorySnapshot = sub.Category,
            IconKeySnapshot = sub.IconKey,
            BrandColorSnapshot = sub.BrandColor,
            TierNameSnapshot = sub.TierName,
            AccountDisplaySnapshot = sub.AccountDisplayCache,
            PaymentChannelSnapshot = sub.PaymentChannelName,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            CreatedAt = nowUtc
        };

        await InsertBillingRecordCoreAsync(conn, tx, record, ct);
        await tx.CommitAsync(ct);
        return record;
    }

    public async Task SkipOccurrenceAsync(string occurrenceId, CancellationToken ct = default)
    {
        await _db.InitializeAsync(ct);
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE BillingOccurrences
            SET Status = $skipped, UpdatedAt = $updatedAt
            WHERE Id = $id AND Status IN ($planned, $pending);
            """;
        cmd.Parameters.AddWithValue("$skipped", (int)OccurrenceStatus.Skipped);
        cmd.Parameters.AddWithValue("$updatedAt", RuqiDatabase.ToIsoTimestamp(_clock.UtcNow));
        cmd.Parameters.AddWithValue("$id", occurrenceId);
        cmd.Parameters.AddWithValue("$planned", (int)OccurrenceStatus.Planned);
        cmd.Parameters.AddWithValue("$pending", (int)OccurrenceStatus.Pending);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<BillingRecord> AddManualBillingRecordAsync(
        string subscriptionId,
        long paidAmountCents,
        DateOnly paidDate,
        string? note = null,
        CancellationToken ct = default)
    {
        await _db.InitializeAsync(ct);
        if (paidAmountCents < 0)
        {
            throw new ArgumentException("实付金额不能为负数。");
        }

        if (paidDate > _clock.Today)
        {
            throw new ArgumentException("补录账单的实际扣款日不能晚于今天。");
        }

        var sub = await GetSubscriptionByIdAsync(subscriptionId, ct)
            ?? throw new InvalidOperationException("未找到该订阅。");

        var record = new BillingRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            OccurrenceId = null,
            SubscriptionId = sub.Id,
            PaidAmountCents = paidAmountCents,
            Currency = "CNY",
            PaidDate = paidDate,
            AppNameSnapshot = sub.AppName,
            CategorySnapshot = sub.Category,
            IconKeySnapshot = sub.IconKey,
            BrandColorSnapshot = sub.BrandColor,
            TierNameSnapshot = sub.TierName,
            AccountDisplaySnapshot = sub.AccountDisplayCache,
            PaymentChannelSnapshot = sub.PaymentChannelName,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            CreatedAt = _clock.UtcNow
        };

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = (SqliteTransaction)await conn.BeginTransactionAsync(ct);
        await InsertBillingRecordCoreAsync(conn, tx, record, ct);
        await tx.CommitAsync(ct);
        return record;
    }

    public async Task<List<BillingRecord>> GetBillingRecordsBySubscriptionAsync(string subscriptionId, CancellationToken ct = default)
    {
        await _db.InitializeAsync(ct);
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM BillingRecords WHERE SubscriptionId = $subId ORDER BY PaidDate DESC, CreatedAt DESC;";
        cmd.Parameters.AddWithValue("$subId", subscriptionId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var list = new List<BillingRecord>();
        while (await reader.ReadAsync(ct))
        {
            list.Add(ReadBillingRecord(reader));
        }

        return list;
    }

    public async Task<BillingRecord?> GetBillingRecordByIdAsync(string recordId, CancellationToken ct = default)
    {
        await _db.InitializeAsync(ct);
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM BillingRecords WHERE Id = $id LIMIT 1;";
        cmd.Parameters.AddWithValue("$id", recordId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? ReadBillingRecord(reader) : null;
    }

    public async Task UpdateBillingRecordAsync(string recordId, long paidAmountCents, DateOnly paidDate, string? note, CancellationToken ct = default)
    {
        await _db.InitializeAsync(ct);
        if (paidAmountCents < 0)
        {
            throw new ArgumentException("实付金额不能为负数。");
        }

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE BillingRecords
            SET PaidAmountCents = $amount, PaidDate = $paidDate, Note = $note
            WHERE Id = $id;
            """;
        cmd.Parameters.AddWithValue("$amount", paidAmountCents);
        cmd.Parameters.AddWithValue("$paidDate", RuqiDatabase.ToIsoDate(paidDate));
        cmd.Parameters.AddWithValue("$note", string.IsNullOrWhiteSpace(note) ? DBNull.Value : note.Trim());
        cmd.Parameters.AddWithValue("$id", recordId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteBillingRecordAsync(string recordId, CancellationToken ct = default)
    {
        await _db.InitializeAsync(ct);
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM BillingRecords WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$id", recordId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    #endregion

    #region Dashboard / Calendar / Statistics / Reminders

    public async Task<DashboardSummary> GetDashboardSummaryAsync(DateOnly? asOfDate = null, CancellationToken ct = default)
    {
        var today = asOfDate ?? _clock.Today;
        await SyncDueOccurrencesAsync(today, ct);

        var subs = await GetSubscriptionsAsync(ct);
        var subMap = subs.ToDictionary(x => x.Id);

        var allOccurrences = await GetAllOccurrencesAsync(ct);
        var pendingList = allOccurrences
            .Where(o => o.Kind == OccurrenceKind.Renewal && o.Status == OccurrenceStatus.Pending && subMap.ContainsKey(o.SubscriptionId))
            .OrderBy(o => o.ScheduledDate)
            .ThenByDescending(o => o.ExpectedAmountCents)
            .Select(o => new PendingOccurrenceView
            {
                Occurrence = o,
                Subscription = subMap[o.SubscriptionId],
                IsToday = o.ScheduledDate == today,
                OverdueDays = Math.Max(0, today.DayNumber - o.ScheduledDate.DayNumber)
            })
            .ToList();

        // 已生成期次查找表，避免未来枚举重复计入
        var existingRenewalKeys = allOccurrences
            .Where(o => o.Kind == OccurrenceKind.Renewal)
            .Select(o => (o.SubscriptionId, o.ScheduledDate))
            .ToHashSet();

        var windowEnd = today.AddDays(29);
        long upcoming30DaysCents = 0;

        // 1) 当天仍待确认的续费项计入未来 30 天预计（排除早于 today 的逾期项）
        foreach (var p in pendingList.Where(x => x.Occurrence.ScheduledDate >= today && x.Occurrence.ScheduledDate <= windowEnd))
        {
            upcoming30DaysCents += p.Occurrence.ExpectedAmountCents;
        }

        // 2) 枚举未来 30 天计划续费期次（同一订阅多笔均计入）
        var upcomingItems = new List<UpcomingRenewalView>();
        var futureHorizonEnd = today.AddDays(365);

        foreach (var sub in subs.Where(s => s.Status == SubscriptionStatus.Active && s.CycleType != BillingCycleType.OneTime))
        {
            foreach (var dt in CycleCalculator.EnumerateFutureScheduledDates(sub, today, windowEnd))
            {
                if (!existingRenewalKeys.Contains((sub.Id, dt)))
                {
                    upcoming30DaysCents += sub.AmountCents;
                }
            }

            // 为首页「即将续费」列表取每条活跃订阅的下一笔计划日期
            if (sub.NextBillingDate >= today && sub.NextBillingDate <= futureHorizonEnd)
            {
                upcomingItems.Add(new UpcomingRenewalView
                {
                    Subscription = sub,
                    ScheduledDate = sub.NextBillingDate,
                    ExpectedAmountCents = sub.AmountCents,
                    DaysUntil = sub.NextBillingDate.DayNumber - today.DayNumber
                });
            }
        }

        upcomingItems = upcomingItems
            .OrderBy(x => x.ScheduledDate)
            .ThenByDescending(x => x.ExpectedAmountCents)
            .ThenBy(x => x.Subscription.AppName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        // 弧形倒计时卡片：优先今天待确认，其次未来最近一天
        UpcomingRenewalView? hero = null;
        var heroSameDayCount = 0;
        var todayPending = pendingList.Where(p => p.IsToday).ToList();
        if (todayPending.Count > 0)
        {
            var top = todayPending
                .OrderByDescending(p => p.Occurrence.ExpectedAmountCents)
                .ThenBy(p => p.Subscription.AppName, StringComparer.CurrentCultureIgnoreCase)
                .First();
            hero = new UpcomingRenewalView
            {
                Subscription = top.Subscription,
                ScheduledDate = today,
                ExpectedAmountCents = top.Occurrence.ExpectedAmountCents,
                DaysUntil = 0,
                PendingOccurrenceId = top.Occurrence.Id
            };
            heroSameDayCount = todayPending.Count;
        }
        else if (upcomingItems.Count > 0)
        {
            var nearestDate = upcomingItems[0].ScheduledDate;
            var sameDay = upcomingItems.Where(x => x.ScheduledDate == nearestDate).ToList();
            hero = sameDay[0];
            heroSameDayCount = sameDay.Count;
        }

        // 本月已付
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var monthEnd = new DateOnly(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));
        var monthRecords = await GetBillingRecordsByDateRangeAsync(monthStart, monthEnd, ct);
        var paidThisMonthCents = monthRecords.Sum(r => r.PaidAmountCents);

        // 月均成本
        var monthlyAvgCents = subs.Sum(CycleCalculator.EstimateMonthlyCostCents);

        return new DashboardSummary
        {
            Today = today,
            HeroRenewal = hero,
            HeroSameDayCount = heroSameDayCount,
            PaidThisMonthCents = paidThisMonthCents,
            Upcoming30DaysCents = upcoming30DaysCents,
            MonthlyAverageCostCents = monthlyAvgCents,
            PendingOccurrences = pendingList,
            UpcomingRenewals = upcomingItems.Take(10).ToList(),
            HasAnySubscription = subs.Count > 0
        };
    }

    public async Task<List<CalendarEventItem>> GetCalendarEventsForMonthAsync(int year, int month, CancellationToken ct = default)
    {
        await SyncDueOccurrencesAsync(ct: ct);

        var monthStart = new DateOnly(year, month, 1);
        var monthEnd = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
        var subs = await GetSubscriptionsAsync(ct);
        var subMap = subs.ToDictionary(x => x.Id);

        var events = new List<CalendarEventItem>();

        // 1) 已扣款（按实际扣款日）
        var records = await GetBillingRecordsByDateRangeAsync(monthStart, monthEnd, ct);
        foreach (var rec in records)
        {
            events.Add(new CalendarEventItem
            {
                Id = $"rec_{rec.Id}",
                SubscriptionId = rec.SubscriptionId,
                OccurrenceId = rec.OccurrenceId,
                RecordId = rec.Id,
                EventDate = rec.PaidDate,
                Kind = OccurrenceKind.Renewal,
                Status = OccurrenceStatus.Confirmed,
                AppName = rec.AppNameSnapshot,
                Category = rec.CategorySnapshot,
                IconKey = rec.IconKeySnapshot,
                BrandColor = rec.BrandColorSnapshot,
                TierName = rec.TierNameSnapshot,
                AccountDisplay = rec.AccountDisplaySnapshot,
                PaymentChannelName = rec.PaymentChannelSnapshot,
                AmountCents = rec.PaidAmountCents,
                CycleLabel = "已扣款"
            });
        }

        // 2) 待确认 & 仅到期（按计划/到期日）
        var allOccurrences = await GetAllOccurrencesAsync(ct);
        var existingRenewalKeys = allOccurrences
            .Where(o => o.Kind == OccurrenceKind.Renewal)
            .Select(o => (o.SubscriptionId, o.ScheduledDate))
            .ToHashSet();

        foreach (var occ in allOccurrences.Where(o => o.ScheduledDate >= monthStart && o.ScheduledDate <= monthEnd))
        {
            if (!subMap.TryGetValue(occ.SubscriptionId, out var sub))
            {
                continue;
            }

            if (occ.Kind == OccurrenceKind.Renewal && occ.Status == OccurrenceStatus.Pending)
            {
                events.Add(new CalendarEventItem
                {
                    Id = $"occ_{occ.Id}",
                    SubscriptionId = sub.Id,
                    OccurrenceId = occ.Id,
                    EventDate = occ.ScheduledDate,
                    Kind = OccurrenceKind.Renewal,
                    Status = OccurrenceStatus.Pending,
                    AppName = sub.AppName,
                    Category = sub.Category,
                    IconKey = sub.IconKey,
                    BrandColor = sub.BrandColor,
                    TierName = sub.TierName,
                    AccountDisplay = sub.AccountDisplayCache,
                    PaymentChannelName = sub.PaymentChannelName,
                    AmountCents = occ.ExpectedAmountCents,
                    CycleLabel = FormatHelpers.FormatCycleName(sub.CycleType, sub.CycleInterval)
                });
            }
            else if (occ.Kind == OccurrenceKind.ExpiryOnly)
            {
                events.Add(new CalendarEventItem
                {
                    Id = $"exp_{occ.Id}",
                    SubscriptionId = sub.Id,
                    OccurrenceId = occ.Id,
                    EventDate = occ.ScheduledDate,
                    Kind = OccurrenceKind.ExpiryOnly,
                    Status = OccurrenceStatus.Planned,
                    AppName = sub.AppName,
                    Category = sub.Category,
                    IconKey = sub.IconKey,
                    BrandColor = sub.BrandColor,
                    TierName = sub.TierName,
                    AccountDisplay = sub.AccountDisplayCache,
                    PaymentChannelName = sub.PaymentChannelName,
                    AmountCents = occ.ExpectedAmountCents,
                    CycleLabel = "到期提醒"
                });
            }
        }

        // 3) 未来计划续费（按计划日期）
        foreach (var sub in subs.Where(s => s.Status == SubscriptionStatus.Active && s.CycleType != BillingCycleType.OneTime))
        {
            foreach (var dt in CycleCalculator.EnumerateFutureScheduledDates(sub, monthStart, monthEnd))
            {
                if (existingRenewalKeys.Contains((sub.Id, dt)))
                {
                    continue;
                }

                events.Add(new CalendarEventItem
                {
                    Id = $"plan_{sub.Id}_{dt:yyyyMMdd}",
                    SubscriptionId = sub.Id,
                    EventDate = dt,
                    Kind = OccurrenceKind.Renewal,
                    Status = OccurrenceStatus.Planned,
                    AppName = sub.AppName,
                    Category = sub.Category,
                    IconKey = sub.IconKey,
                    BrandColor = sub.BrandColor,
                    TierName = sub.TierName,
                    AccountDisplay = sub.AccountDisplayCache,
                    PaymentChannelName = sub.PaymentChannelName,
                    AmountCents = sub.AmountCents,
                    CycleLabel = FormatHelpers.FormatCycleName(sub.CycleType, sub.CycleInterval)
                });
            }
        }

        return events
            .OrderBy(e => e.EventDate)
            .ThenBy(e => e.Status == OccurrenceStatus.Pending ? 0 : e.Kind == OccurrenceKind.Renewal && e.Status == OccurrenceStatus.Planned ? 1 : e.Status == OccurrenceStatus.Confirmed ? 2 : 3)
            .ThenByDescending(e => e.AmountCents)
            .ToList();
    }

    public async Task<StatisticsSummary> GetStatisticsSummaryAsync(bool isYearly, int year, int month, CancellationToken ct = default)
    {
        await _db.InitializeAsync(ct);

        DateOnly periodStart;
        DateOnly periodEnd;
        DateOnly trendStart;
        DateOnly trendEnd;

        if (isYearly)
        {
            periodStart = new DateOnly(year, 1, 1);
            periodEnd = new DateOnly(year, 12, 31);
            trendStart = periodStart;
            trendEnd = periodEnd;
        }
        else
        {
            periodStart = new DateOnly(year, month, 1);
            periodEnd = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
            var sixMonthsAgo = periodStart.AddMonths(-5);
            trendStart = new DateOnly(sixMonthsAgo.Year, sixMonthsAgo.Month, 1);
            trendEnd = periodEnd;
        }

        var trendRecords = await GetBillingRecordsByDateRangeAsync(trendStart, trendEnd, ct);
        var periodRecords = trendRecords.Where(r => r.PaidDate >= periodStart && r.PaidDate <= periodEnd).ToList();

        var trendPoints = new List<TrendBarPoint>();
        if (isYearly)
        {
            for (var m = 1; m <= 12; m++)
            {
                var sum = trendRecords.Where(r => r.PaidDate.Year == year && r.PaidDate.Month == m).Sum(r => r.PaidAmountCents);
                trendPoints.Add(new TrendBarPoint
                {
                    Label = $"{m}月",
                    Year = year,
                    Month = m,
                    AmountCents = sum,
                    IsHighlighted = m == _clock.Today.Month && year == _clock.Today.Year
                });
            }
        }
        else
        {
            for (var i = 5; i >= 0; i--)
            {
                var d = periodStart.AddMonths(-i);
                var sum = trendRecords.Where(r => r.PaidDate.Year == d.Year && r.PaidDate.Month == d.Month).Sum(r => r.PaidAmountCents);
                trendPoints.Add(new TrendBarPoint
                {
                    Label = $"{d.Month}月",
                    Year = d.Year,
                    Month = d.Month,
                    AmountCents = sum,
                    IsHighlighted = i == 0
                });
            }
        }

        var totalPaidCents = periodRecords.Sum(r => r.PaidAmountCents);
        var categories = periodRecords
            .GroupBy(r => string.IsNullOrWhiteSpace(r.CategorySnapshot) ? BuiltInCategories.Other : r.CategorySnapshot)
            .Select(catGroup =>
            {
                var catTotal = catGroup.Sum(x => x.PaidAmountCents);
                var apps = catGroup
                    .GroupBy(x => x.AppNameSnapshot)
                    .Select(appGroup =>
                    {
                        var first = appGroup.First();
                        var accounts = appGroup
                            .GroupBy(x => (Account: string.IsNullOrWhiteSpace(x.AccountDisplaySnapshot) ? "默认账号" : x.AccountDisplaySnapshot!, Deleted: x.SubscriptionId is null))
                            .Select(accGroup => new AccountStatItem
                            {
                                AccountDisplay = accGroup.Key.Account,
                                IsDeletedSubscription = accGroup.Key.Deleted,
                                TotalCents = accGroup.Sum(x => x.PaidAmountCents),
                                RecordCount = accGroup.Count(),
                                Records = accGroup.OrderByDescending(x => x.PaidDate).ToList()
                            })
                            .OrderByDescending(x => x.TotalCents)
                            .ToList();

                        return new AppStatItem
                        {
                            AppName = appGroup.Key,
                            IconKey = first.IconKeySnapshot,
                            BrandColor = first.BrandColorSnapshot,
                            TotalCents = appGroup.Sum(x => x.PaidAmountCents),
                            HasDeletedRecords = appGroup.Any(x => x.SubscriptionId is null),
                            Accounts = accounts,
                            Records = appGroup.OrderByDescending(x => x.PaidDate).ToList()
                        };
                    })
                    .OrderByDescending(x => x.TotalCents)
                    .ToList();

                return new CategoryStatItem
                {
                    Category = catGroup.Key,
                    TotalCents = catTotal,
                    Ratio = totalPaidCents > 0 ? (double)catTotal / totalPaidCents : 0,
                    Apps = apps
                };
            })
            .OrderByDescending(x => x.TotalCents)
            .ToList();

        var subs = await GetSubscriptionsAsync(ct);
        var monthlyAvgCents = subs.Sum(CycleCalculator.EstimateMonthlyCostCents);

        return new StatisticsSummary
        {
            IsYearly = isYearly,
            Year = year,
            Month = month,
            TotalPaidCents = totalPaidCents,
            MonthlyAverageCostCents = monthlyAvgCents,
            TrendPoints = trendPoints,
            Categories = categories
        };
    }

    public async Task<List<ScheduledReminderItem>> BuildUpcomingRemindersAsync(
        DateTime? nowLocalOverride = null,
        int horizonDays = 60,
        CancellationToken ct = default)
    {
        var nowLocal = nowLocalOverride ?? _clock.NowLocal;
        var today = DateOnly.FromDateTime(nowLocal);
        var horizonEnd = today.AddDays(Math.Max(1, horizonDays));

        await SyncDueOccurrencesAsync(today, ct);

        var subs = await GetSubscriptionsAsync(ct);
        var occurrences = await GetAllOccurrencesAsync(ct);
        var items = new List<ScheduledReminderItem>();

        foreach (var sub in subs)
        {
            if (!sub.RemindBeforeEnabled && !sub.RemindOnDayEnabled)
            {
                continue;
            }

            var accountSuffix = string.IsNullOrWhiteSpace(sub.AccountDisplayCache)
                ? string.Empty
                : $"（{sub.AccountDisplayCache}）";

            if (sub.Status == SubscriptionStatus.Active && sub.CycleType != BillingCycleType.OneTime)
            {
                // 今天待确认 + 未来 60 天计划续费
                var subOccurrences = occurrences
                    .Where(o => o.SubscriptionId == sub.Id && o.Kind == OccurrenceKind.Renewal)
                    .ToList();

                var handledDates = subOccurrences
                    .Where(o => o.Status is OccurrenceStatus.Confirmed or OccurrenceStatus.Skipped)
                    .Select(o => o.ScheduledDate)
                    .ToHashSet();

                var pendingByDate = subOccurrences
                    .Where(o => o.Status == OccurrenceStatus.Pending && o.ScheduledDate >= today && o.ScheduledDate <= horizonEnd)
                    .ToDictionary(o => o.ScheduledDate, o => o);

                var targetDates = new SortedSet<DateOnly>(pendingByDate.Keys);
                foreach (var dt in CycleCalculator.EnumerateFutureScheduledDates(sub, today, horizonEnd))
                {
                    if (!handledDates.Contains(dt))
                    {
                        targetDates.Add(dt);
                    }
                }

                foreach (var targetDate in targetDates)
                {
                    pendingByDate.TryGetValue(targetDate, out var pendingOcc);
                    var amountText = FormatHelpers.FormatCny(pendingOcc?.ExpectedAmountCents ?? sub.AmountCents);

                    if (sub.RemindBeforeEnabled)
                    {
                        var beforeDays = Math.Max(1, sub.RemindBeforeDays);
                        var fireAt = targetDate.AddDays(-beforeDays).ToDateTime(sub.RemindTime);
                        if (fireAt > nowLocal)
                        {
                            items.Add(new ScheduledReminderItem
                            {
                                NotificationId = ComputeStableNotificationId($"{sub.Id}|{targetDate:yyyyMMdd}|before"),
                                SubscriptionId = sub.Id,
                                OccurrenceId = pendingOcc?.Id,
                                Kind = ReminderKind.RenewalBefore,
                                TargetDate = targetDate,
                                FireAtLocal = fireAt,
                                Title = $"{sub.AppName}{accountSuffix} 将在 {beforeDays} 天后续费",
                                Body = $"预计 {FormatHelpers.FormatMonthDay(targetDate)} 扣款 {amountText}，如不再需要请提前取消。"
                            });
                        }
                    }

                    if (sub.RemindOnDayEnabled)
                    {
                        var fireAt = targetDate.ToDateTime(sub.RemindTime);
                        if (fireAt > nowLocal)
                        {
                            items.Add(new ScheduledReminderItem
                            {
                                NotificationId = ComputeStableNotificationId($"{sub.Id}|{targetDate:yyyyMMdd}|onday"),
                                SubscriptionId = sub.Id,
                                OccurrenceId = pendingOcc?.Id,
                                Kind = ReminderKind.RenewalOnDay,
                                TargetDate = targetDate,
                                FireAtLocal = fireAt,
                                Title = $"{sub.AppName}{accountSuffix} 今日预计续费",
                                Body = $"预计扣款 {amountText}，扣款后请打开「如期」确认记账。"
                            });
                        }
                    }
                }
            }
            else if (sub.ExpiryDate.HasValue && sub.ExpiryDate.Value >= today && sub.ExpiryDate.Value <= horizonEnd)
            {
                var expDate = sub.ExpiryDate.Value;
                if (sub.RemindBeforeEnabled)
                {
                    var beforeDays = Math.Max(1, sub.RemindBeforeDays);
                    var fireAt = expDate.AddDays(-beforeDays).ToDateTime(sub.RemindTime);
                    if (fireAt > nowLocal)
                    {
                        items.Add(new ScheduledReminderItem
                        {
                            NotificationId = ComputeStableNotificationId($"{sub.Id}|{expDate:yyyyMMdd}|exp_before"),
                            SubscriptionId = sub.Id,
                            Kind = ReminderKind.ExpiryBefore,
                            TargetDate = expDate,
                            FireAtLocal = fireAt,
                            Title = $"{sub.AppName}{accountSuffix} 将在 {beforeDays} 天后到期",
                            Body = $"有效期至 {FormatHelpers.FormatMonthDay(expDate)}，请按需安排续费或备份资料。"
                        });
                    }
                }

                if (sub.RemindOnDayEnabled)
                {
                    var fireAt = expDate.ToDateTime(sub.RemindTime);
                    if (fireAt > nowLocal)
                    {
                        items.Add(new ScheduledReminderItem
                        {
                            NotificationId = ComputeStableNotificationId($"{sub.Id}|{expDate:yyyyMMdd}|exp_onday"),
                            SubscriptionId = sub.Id,
                            Kind = ReminderKind.ExpiryOnDay,
                            TargetDate = expDate,
                            FireAtLocal = fireAt,
                            Title = $"{sub.AppName}{accountSuffix} 今日到期",
                            Body = "当前服务有效期截至今日。"
                        });
                    }
                }
            }
        }

        return items.OrderBy(x => x.FireAtLocal).ToList();
    }

    #endregion

    #region Settings

    public async Task<AppSettings> GetSettingsAsync(CancellationToken ct = default)
    {
        await _db.InitializeAsync(ct);
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Key, Value FROM AppSettings;";
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        while (await reader.ReadAsync(ct))
        {
            dict[reader.GetString(0)] = reader.GetString(1);
        }

        var settings = new AppSettings();
        if (dict.TryGetValue("DefaultRemindBeforeEnabled", out var bVal) && bool.TryParse(bVal, out var bParsed))
            settings.DefaultRemindBeforeEnabled = bParsed;
        if (dict.TryGetValue("DefaultRemindBeforeDays", out var dVal) && int.TryParse(dVal, out var dParsed))
            settings.DefaultRemindBeforeDays = Math.Max(1, dParsed);
        if (dict.TryGetValue("DefaultRemindOnDayEnabled", out var oVal) && bool.TryParse(oVal, out var oParsed))
            settings.DefaultRemindOnDayEnabled = oParsed;
        if (dict.TryGetValue("DefaultRemindTime", out var tVal) && TimeOnly.TryParseExact(tVal, "HH:mm", out var tParsed))
            settings.DefaultRemindTime = tParsed;
        if (dict.TryGetValue("LastBackupAt", out var lVal) && DateTimeOffset.TryParse(lVal, out var lParsed))
            settings.LastBackupAt = lParsed;

        return settings;
    }

    public async Task SaveSettingsAsync(AppSettings settings, CancellationToken ct = default)
    {
        await _db.InitializeAsync(ct);
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = (SqliteTransaction)await conn.BeginTransactionAsync(ct);

        await UpsertSettingAsync(conn, tx, "DefaultRemindBeforeEnabled", settings.DefaultRemindBeforeEnabled.ToString(), ct);
        await UpsertSettingAsync(conn, tx, "DefaultRemindBeforeDays", Math.Max(1, settings.DefaultRemindBeforeDays).ToString(), ct);
        await UpsertSettingAsync(conn, tx, "DefaultRemindOnDayEnabled", settings.DefaultRemindOnDayEnabled.ToString(), ct);
        await UpsertSettingAsync(conn, tx, "DefaultRemindTime", RuqiDatabase.ToTimeText(settings.DefaultRemindTime), ct);
        if (settings.LastBackupAt.HasValue)
        {
            await UpsertSettingAsync(conn, tx, "LastBackupAt", RuqiDatabase.ToIsoTimestamp(settings.LastBackupAt.Value), ct);
        }

        await tx.CommitAsync(ct);
    }

    #endregion

    #region Internal Helpers

    internal async Task<List<BillingOccurrence>> GetAllOccurrencesAsync(CancellationToken ct = default)
    {
        await _db.InitializeAsync(ct);
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM BillingOccurrences ORDER BY ScheduledDate ASC;";
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var list = new List<BillingOccurrence>();
        while (await reader.ReadAsync(ct))
        {
            list.Add(ReadOccurrence(reader));
        }

        return list;
    }

    internal async Task<List<BillingRecord>> GetAllBillingRecordsAsync(CancellationToken ct = default)
    {
        await _db.InitializeAsync(ct);
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM BillingRecords ORDER BY PaidDate DESC, CreatedAt DESC;";
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var list = new List<BillingRecord>();
        while (await reader.ReadAsync(ct))
        {
            list.Add(ReadBillingRecord(reader));
        }

        return list;
    }

    private async Task<List<BillingRecord>> GetBillingRecordsByDateRangeAsync(DateOnly start, DateOnly end, CancellationToken ct)
    {
        await _db.InitializeAsync(ct);
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT * FROM BillingRecords
            WHERE PaidDate >= $start AND PaidDate <= $end
            ORDER BY PaidDate DESC, CreatedAt DESC;
            """;
        cmd.Parameters.AddWithValue("$start", RuqiDatabase.ToIsoDate(start));
        cmd.Parameters.AddWithValue("$end", RuqiDatabase.ToIsoDate(end));
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var list = new List<BillingRecord>();
        while (await reader.ReadAsync(ct))
        {
            list.Add(ReadBillingRecord(reader));
        }

        return list;
    }

    private async Task EnsureCustomCatalogIfMissingAsync(Subscription sub, CancellationToken ct)
    {
        var allCatalog = await SearchCatalogAsync(ct: ct);
        var exists = allCatalog.Any(c =>
            string.Equals(c.AppName, sub.AppName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(c.TierName ?? string.Empty, sub.TierName ?? string.Empty, StringComparison.OrdinalIgnoreCase));

        if (!exists)
        {
            await SaveCustomCatalogItemAsync(new CatalogItem
            {
                AppName = sub.AppName,
                Category = sub.Category,
                IconKey = sub.IconKey,
                BrandColor = sub.BrandColor,
                TierName = sub.TierName,
                DefaultCycleType = sub.CycleType,
                DefaultInterval = sub.CycleInterval,
                ReferencePriceCents = sub.AmountCents
            }, ct);
        }
    }

    private static async Task<bool> EnsurePendingOccurrenceAsync(
        SqliteConnection conn,
        SqliteTransaction tx,
        Subscription sub,
        DateOnly scheduledDate,
        DateTimeOffset nowUtc,
        CancellationToken ct)
    {
        await using (var q = conn.CreateCommand())
        {
            q.Transaction = tx;
            q.CommandText = """
                SELECT Id, Status FROM BillingOccurrences
                WHERE SubscriptionId = $subId AND ScheduledDate = $date AND Kind = $kind
                LIMIT 1;
                """;
            q.Parameters.AddWithValue("$subId", sub.Id);
            q.Parameters.AddWithValue("$date", RuqiDatabase.ToIsoDate(scheduledDate));
            q.Parameters.AddWithValue("$kind", (int)OccurrenceKind.Renewal);
            await using var reader = await q.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                var id = reader.GetString(0);
                var status = (OccurrenceStatus)reader.GetInt32(1);
                if (status == OccurrenceStatus.Planned)
                {
                    await reader.DisposeAsync();
                    await using var upd = conn.CreateCommand();
                    upd.Transaction = tx;
                    upd.CommandText = "UPDATE BillingOccurrences SET Status = $pending, UpdatedAt = $updatedAt WHERE Id = $id;";
                    upd.Parameters.AddWithValue("$pending", (int)OccurrenceStatus.Pending);
                    upd.Parameters.AddWithValue("$updatedAt", RuqiDatabase.ToIsoTimestamp(nowUtc));
                    upd.Parameters.AddWithValue("$id", id);
                    await upd.ExecuteNonQueryAsync(ct);
                }

                return false;
            }
        }

        await InsertOccurrenceIgnoreAsync(conn, tx, new BillingOccurrence
        {
            Id = Guid.NewGuid().ToString("N"),
            SubscriptionId = sub.Id,
            ScheduledDate = scheduledDate,
            ExpectedAmountCents = sub.AmountCents,
            Kind = OccurrenceKind.Renewal,
            Status = OccurrenceStatus.Pending,
            CreatedAt = nowUtc,
            UpdatedAt = nowUtc
        }, ct);

        return true;
    }

    internal static async Task UpsertSubscriptionCoreAsync(SqliteConnection conn, SqliteTransaction tx, Subscription sub, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO Subscriptions
                (Id, AppName, Category, IconKey, BrandColor, TierName, AmountCents, Currency,
                 CycleType, CycleInterval, AnchorDay, AnchorMonth, NextBillingDate, ExpiryDate,
                 AccountId, AccountDisplayCache, PaymentChannelName, Status,
                 RemindBeforeEnabled, RemindBeforeDays, RemindOnDayEnabled, RemindTime,
                 Note, CreatedAt, UpdatedAt)
            VALUES
                ($id, $appName, $category, $iconKey, $brandColor, $tierName, $amountCents, $currency,
                 $cycleType, $cycleInterval, $anchorDay, $anchorMonth, $nextDate, $expiryDate,
                 $accountId, $accountDisplay, $channel, $status,
                 $remBefore, $remDays, $remOnDay, $remTime,
                 $note, $createdAt, $updatedAt)
            ON CONFLICT(Id) DO UPDATE SET
                AppName = excluded.AppName,
                Category = excluded.Category,
                IconKey = excluded.IconKey,
                BrandColor = excluded.BrandColor,
                TierName = excluded.TierName,
                AmountCents = excluded.AmountCents,
                Currency = excluded.Currency,
                CycleType = excluded.CycleType,
                CycleInterval = excluded.CycleInterval,
                AnchorDay = excluded.AnchorDay,
                AnchorMonth = excluded.AnchorMonth,
                NextBillingDate = excluded.NextBillingDate,
                ExpiryDate = excluded.ExpiryDate,
                AccountId = excluded.AccountId,
                AccountDisplayCache = excluded.AccountDisplayCache,
                PaymentChannelName = excluded.PaymentChannelName,
                Status = excluded.Status,
                RemindBeforeEnabled = excluded.RemindBeforeEnabled,
                RemindBeforeDays = excluded.RemindBeforeDays,
                RemindOnDayEnabled = excluded.RemindOnDayEnabled,
                RemindTime = excluded.RemindTime,
                Note = excluded.Note,
                UpdatedAt = excluded.UpdatedAt;
            """;
        cmd.Parameters.AddWithValue("$id", sub.Id);
        cmd.Parameters.AddWithValue("$appName", sub.AppName);
        cmd.Parameters.AddWithValue("$category", sub.Category);
        cmd.Parameters.AddWithValue("$iconKey", sub.IconKey ?? string.Empty);
        cmd.Parameters.AddWithValue("$brandColor", sub.BrandColor ?? "#5B6CF9");
        cmd.Parameters.AddWithValue("$tierName", (object?)sub.TierName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$amountCents", sub.AmountCents);
        cmd.Parameters.AddWithValue("$currency", sub.Currency);
        cmd.Parameters.AddWithValue("$cycleType", (int)sub.CycleType);
        cmd.Parameters.AddWithValue("$cycleInterval", sub.CycleInterval);
        cmd.Parameters.AddWithValue("$anchorDay", sub.AnchorDay);
        cmd.Parameters.AddWithValue("$anchorMonth", sub.AnchorMonth);
        cmd.Parameters.AddWithValue("$nextDate", RuqiDatabase.ToIsoDate(sub.NextBillingDate));
        cmd.Parameters.AddWithValue("$expiryDate", sub.ExpiryDate.HasValue ? RuqiDatabase.ToIsoDate(sub.ExpiryDate.Value) : DBNull.Value);
        cmd.Parameters.AddWithValue("$accountId", (object?)sub.AccountId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$accountDisplay", (object?)sub.AccountDisplayCache ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$channel", (object?)sub.PaymentChannelName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$status", (int)sub.Status);
        cmd.Parameters.AddWithValue("$remBefore", sub.RemindBeforeEnabled ? 1 : 0);
        cmd.Parameters.AddWithValue("$remDays", sub.RemindBeforeDays);
        cmd.Parameters.AddWithValue("$remOnDay", sub.RemindOnDayEnabled ? 1 : 0);
        cmd.Parameters.AddWithValue("$remTime", RuqiDatabase.ToTimeText(sub.RemindTime));
        cmd.Parameters.AddWithValue("$note", (object?)sub.Note ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$createdAt", RuqiDatabase.ToIsoTimestamp(sub.CreatedAt));
        cmd.Parameters.AddWithValue("$updatedAt", RuqiDatabase.ToIsoTimestamp(sub.UpdatedAt));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    internal static async Task InsertOccurrenceIgnoreAsync(SqliteConnection conn, SqliteTransaction tx, BillingOccurrence occ, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT OR IGNORE INTO BillingOccurrences
                (Id, SubscriptionId, ScheduledDate, ExpectedAmountCents, Kind, Status, CreatedAt, UpdatedAt)
            VALUES
                ($id, $subId, $date, $amount, $kind, $status, $createdAt, $updatedAt);
            """;
        cmd.Parameters.AddWithValue("$id", occ.Id);
        cmd.Parameters.AddWithValue("$subId", occ.SubscriptionId);
        cmd.Parameters.AddWithValue("$date", RuqiDatabase.ToIsoDate(occ.ScheduledDate));
        cmd.Parameters.AddWithValue("$amount", occ.ExpectedAmountCents);
        cmd.Parameters.AddWithValue("$kind", (int)occ.Kind);
        cmd.Parameters.AddWithValue("$status", (int)occ.Status);
        cmd.Parameters.AddWithValue("$createdAt", RuqiDatabase.ToIsoTimestamp(occ.CreatedAt));
        cmd.Parameters.AddWithValue("$updatedAt", RuqiDatabase.ToIsoTimestamp(occ.UpdatedAt));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    internal static async Task InsertBillingRecordCoreAsync(SqliteConnection conn, SqliteTransaction tx, BillingRecord rec, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO BillingRecords
                (Id, OccurrenceId, SubscriptionId, PaidAmountCents, Currency, PaidDate,
                 AppNameSnapshot, CategorySnapshot, IconKeySnapshot, BrandColorSnapshot,
                 TierNameSnapshot, AccountDisplaySnapshot, PaymentChannelSnapshot, Note, CreatedAt)
            VALUES
                ($id, $occId, $subId, $amount, $currency, $paidDate,
                 $appName, $category, $iconKey, $brandColor,
                 $tierName, $accountDisplay, $channel, $note, $createdAt);
            """;
        cmd.Parameters.AddWithValue("$id", rec.Id);
        cmd.Parameters.AddWithValue("$occId", (object?)rec.OccurrenceId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$subId", (object?)rec.SubscriptionId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$amount", rec.PaidAmountCents);
        cmd.Parameters.AddWithValue("$currency", rec.Currency);
        cmd.Parameters.AddWithValue("$paidDate", RuqiDatabase.ToIsoDate(rec.PaidDate));
        cmd.Parameters.AddWithValue("$appName", rec.AppNameSnapshot);
        cmd.Parameters.AddWithValue("$category", rec.CategorySnapshot);
        cmd.Parameters.AddWithValue("$iconKey", rec.IconKeySnapshot ?? string.Empty);
        cmd.Parameters.AddWithValue("$brandColor", rec.BrandColorSnapshot ?? "#5B6CF9");
        cmd.Parameters.AddWithValue("$tierName", (object?)rec.TierNameSnapshot ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$accountDisplay", (object?)rec.AccountDisplaySnapshot ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$channel", (object?)rec.PaymentChannelSnapshot ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$note", (object?)rec.Note ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$createdAt", RuqiDatabase.ToIsoTimestamp(rec.CreatedAt));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    internal static async Task UpsertSettingAsync(SqliteConnection conn, SqliteTransaction tx, string key, string value, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO AppSettings (Key, Value) VALUES ($k, $v)
            ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value;
            """;
        cmd.Parameters.AddWithValue("$k", key);
        cmd.Parameters.AddWithValue("$v", value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static int ComputeStableNotificationId(string key)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (var c in key)
            {
                hash ^= c;
                hash *= 16777619;
            }

            return (int)(hash & 0x7FFFFFFF);
        }
    }

    private static CatalogItem ReadCatalogItem(SqliteDataReader r) => new()
    {
        Id = r.GetString(r.GetOrdinal("Id")),
        IsBuiltIn = r.GetInt32(r.GetOrdinal("IsBuiltIn")) != 0,
        AppName = r.GetString(r.GetOrdinal("AppName")),
        Category = r.GetString(r.GetOrdinal("Category")),
        IconKey = r.GetString(r.GetOrdinal("IconKey")),
        BrandColor = r.GetString(r.GetOrdinal("BrandColor")),
        TierName = r.IsDBNull(r.GetOrdinal("TierName")) ? null : r.GetString(r.GetOrdinal("TierName")),
        DefaultCycleType = (BillingCycleType)r.GetInt32(r.GetOrdinal("DefaultCycleType")),
        DefaultInterval = r.GetInt32(r.GetOrdinal("DefaultInterval")),
        ReferencePriceCents = r.IsDBNull(r.GetOrdinal("ReferencePriceCents")) ? null : r.GetInt64(r.GetOrdinal("ReferencePriceCents")),
        SearchKeywords = r.GetString(r.GetOrdinal("SearchKeywords"))
    };

    private static AccountProfile ReadAccountProfile(SqliteDataReader r) => new()
    {
        Id = r.GetString(r.GetOrdinal("Id")),
        AppName = r.GetString(r.GetOrdinal("AppName")),
        Nickname = r.IsDBNull(r.GetOrdinal("Nickname")) ? null : r.GetString(r.GetOrdinal("Nickname")),
        Identifier = r.IsDBNull(r.GetOrdinal("Identifier")) ? null : r.GetString(r.GetOrdinal("Identifier"))
    };

    private static PaymentChannel ReadPaymentChannel(SqliteDataReader r) => new()
    {
        Id = r.GetString(r.GetOrdinal("Id")),
        Name = r.GetString(r.GetOrdinal("Name")),
        IsBuiltIn = r.GetInt32(r.GetOrdinal("IsBuiltIn")) != 0,
        SortOrder = r.GetInt32(r.GetOrdinal("SortOrder")),
        IsArchived = r.GetInt32(r.GetOrdinal("IsArchived")) != 0
    };

    private static Subscription ReadSubscription(SqliteDataReader r) => new()
    {
        Id = r.GetString(r.GetOrdinal("Id")),
        AppName = r.GetString(r.GetOrdinal("AppName")),
        Category = r.GetString(r.GetOrdinal("Category")),
        IconKey = r.GetString(r.GetOrdinal("IconKey")),
        BrandColor = r.GetString(r.GetOrdinal("BrandColor")),
        TierName = r.IsDBNull(r.GetOrdinal("TierName")) ? null : r.GetString(r.GetOrdinal("TierName")),
        AmountCents = r.GetInt64(r.GetOrdinal("AmountCents")),
        Currency = r.GetString(r.GetOrdinal("Currency")),
        CycleType = (BillingCycleType)r.GetInt32(r.GetOrdinal("CycleType")),
        CycleInterval = r.GetInt32(r.GetOrdinal("CycleInterval")),
        AnchorDay = r.GetInt32(r.GetOrdinal("AnchorDay")),
        AnchorMonth = r.GetInt32(r.GetOrdinal("AnchorMonth")),
        NextBillingDate = RuqiDatabase.FromIsoDate(r.GetString(r.GetOrdinal("NextBillingDate"))),
        ExpiryDate = r.IsDBNull(r.GetOrdinal("ExpiryDate")) ? null : RuqiDatabase.FromIsoDate(r.GetString(r.GetOrdinal("ExpiryDate"))),
        AccountId = r.IsDBNull(r.GetOrdinal("AccountId")) ? null : r.GetString(r.GetOrdinal("AccountId")),
        AccountDisplayCache = r.IsDBNull(r.GetOrdinal("AccountDisplayCache")) ? null : r.GetString(r.GetOrdinal("AccountDisplayCache")),
        PaymentChannelName = r.IsDBNull(r.GetOrdinal("PaymentChannelName")) ? null : r.GetString(r.GetOrdinal("PaymentChannelName")),
        Status = (SubscriptionStatus)r.GetInt32(r.GetOrdinal("Status")),
        RemindBeforeEnabled = r.GetInt32(r.GetOrdinal("RemindBeforeEnabled")) != 0,
        RemindBeforeDays = r.GetInt32(r.GetOrdinal("RemindBeforeDays")),
        RemindOnDayEnabled = r.GetInt32(r.GetOrdinal("RemindOnDayEnabled")) != 0,
        RemindTime = RuqiDatabase.FromTimeText(r.GetString(r.GetOrdinal("RemindTime"))),
        Note = r.IsDBNull(r.GetOrdinal("Note")) ? null : r.GetString(r.GetOrdinal("Note")),
        CreatedAt = RuqiDatabase.FromIsoTimestamp(r.GetString(r.GetOrdinal("CreatedAt"))),
        UpdatedAt = RuqiDatabase.FromIsoTimestamp(r.GetString(r.GetOrdinal("UpdatedAt")))
    };

    private static BillingOccurrence ReadOccurrence(SqliteDataReader r) => new()
    {
        Id = r.GetString(r.GetOrdinal("Id")),
        SubscriptionId = r.GetString(r.GetOrdinal("SubscriptionId")),
        ScheduledDate = RuqiDatabase.FromIsoDate(r.GetString(r.GetOrdinal("ScheduledDate"))),
        ExpectedAmountCents = r.GetInt64(r.GetOrdinal("ExpectedAmountCents")),
        Kind = (OccurrenceKind)r.GetInt32(r.GetOrdinal("Kind")),
        Status = (OccurrenceStatus)r.GetInt32(r.GetOrdinal("Status")),
        CreatedAt = RuqiDatabase.FromIsoTimestamp(r.GetString(r.GetOrdinal("CreatedAt"))),
        UpdatedAt = RuqiDatabase.FromIsoTimestamp(r.GetString(r.GetOrdinal("UpdatedAt")))
    };

    private static BillingRecord ReadBillingRecord(SqliteDataReader r) => new()
    {
        Id = r.GetString(r.GetOrdinal("Id")),
        OccurrenceId = r.IsDBNull(r.GetOrdinal("OccurrenceId")) ? null : r.GetString(r.GetOrdinal("OccurrenceId")),
        SubscriptionId = r.IsDBNull(r.GetOrdinal("SubscriptionId")) ? null : r.GetString(r.GetOrdinal("SubscriptionId")),
        PaidAmountCents = r.GetInt64(r.GetOrdinal("PaidAmountCents")),
        Currency = r.GetString(r.GetOrdinal("Currency")),
        PaidDate = RuqiDatabase.FromIsoDate(r.GetString(r.GetOrdinal("PaidDate"))),
        AppNameSnapshot = r.GetString(r.GetOrdinal("AppNameSnapshot")),
        CategorySnapshot = r.GetString(r.GetOrdinal("CategorySnapshot")),
        IconKeySnapshot = r.GetString(r.GetOrdinal("IconKeySnapshot")),
        BrandColorSnapshot = r.GetString(r.GetOrdinal("BrandColorSnapshot")),
        TierNameSnapshot = r.IsDBNull(r.GetOrdinal("TierNameSnapshot")) ? null : r.GetString(r.GetOrdinal("TierNameSnapshot")),
        AccountDisplaySnapshot = r.IsDBNull(r.GetOrdinal("AccountDisplaySnapshot")) ? null : r.GetString(r.GetOrdinal("AccountDisplaySnapshot")),
        PaymentChannelSnapshot = r.IsDBNull(r.GetOrdinal("PaymentChannelSnapshot")) ? null : r.GetString(r.GetOrdinal("PaymentChannelSnapshot")),
        Note = r.IsDBNull(r.GetOrdinal("Note")) ? null : r.GetString(r.GetOrdinal("Note")),
        CreatedAt = RuqiDatabase.FromIsoTimestamp(r.GetString(r.GetOrdinal("CreatedAt")))
    };

    #endregion
}
