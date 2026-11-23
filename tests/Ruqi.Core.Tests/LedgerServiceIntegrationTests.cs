using Ruqi.Core.Data;
using Ruqi.Core.Models;
using Ruqi.Core.Services;

namespace Ruqi.Core.Tests;

internal sealed class FakeClock(DateTime initialLocal) : IClock
{
    public DateTime NowLocal { get; set; } = initialLocal;
    public DateTimeOffset UtcNow => new(DateTime.SpecifyKind(NowLocal, DateTimeKind.Utc));
}

public sealed class LedgerServiceIntegrationTests : IDisposable
{
    private readonly string _tempDir;

    public LedgerServiceIntegrationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ruqi_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    private (RuqiDatabase Db, LedgerService Ledger, BackupService Backup, FakeClock Clock) CreateEnv(DateTime initialLocal)
    {
        var dbPath = Path.Combine(_tempDir, Guid.NewGuid().ToString("N") + ".db");
        var clock = new FakeClock(initialLocal);
        var db = new RuqiDatabase(dbPath);
        var ledger = new LedgerService(db, clock);
        var backup = new BackupService(db, ledger, clock);
        return (db, ledger, backup, clock);
    }

    [Fact]
    public async Task MultiAccountCopy_AndIndependentEdit_WorksOffline()
    {
        var (_, ledger, _, clock) = CreateEnv(new DateTime(2026, 11, 20, 10, 0, 0));

        var catalog = await ledger.SearchCatalogAsync("ChatGPT");
        Assert.NotEmpty(catalog);

        var personal = await ledger.SaveSubscriptionAsync(new SubscriptionDraft
        {
            AppName = "ChatGPT",
            Category = BuiltInCategories.AiTools,
            IconKey = "chatgpt",
            TierName = "Plus",
            AmountCents = 14900,
            CycleType = BillingCycleType.Monthly,
            NextBillingDate = new DateOnly(2026, 11, 26),
            AccountNickname = "个人号",
            AccountIdentifier = "me@icloud.com",
            PaymentChannelName = "App Store"
        });

        var copyDraft = await ledger.CreateCopyDraftAsync(personal.Id);
        Assert.Equal("ChatGPT", copyDraft.AppName);
        Assert.Null(copyDraft.AccountId);
        Assert.Null(copyDraft.AccountNickname);

        copyDraft.AccountNickname = "工作号";
        copyDraft.AccountIdentifier = "work@outlook.com";
        copyDraft.PaymentChannelName = "官网";
        copyDraft.AmountCents = 14500;
        copyDraft.NextBillingDate = new DateOnly(2026, 11, 28);

        var work = await ledger.SaveSubscriptionAsync(copyDraft);

        // 编辑工作号不影响个人号
        var workEdit = new SubscriptionDraft
        {
            Id = work.Id,
            AppName = work.AppName,
            Category = work.Category,
            IconKey = work.IconKey,
            TierName = work.TierName,
            AmountCents = 15200,
            CycleType = work.CycleType,
            NextBillingDate = work.NextBillingDate,
            AccountId = work.AccountId,
            PaymentChannelName = "银行卡/信用卡"
        };
        await ledger.SaveSubscriptionAsync(workEdit);

        var reloadedPersonal = await ledger.GetSubscriptionByIdAsync(personal.Id);
        var reloadedWork = await ledger.GetSubscriptionByIdAsync(work.Id);

        Assert.NotNull(reloadedPersonal);
        Assert.NotNull(reloadedWork);
        Assert.Equal(14900, reloadedPersonal!.AmountCents);
        Assert.Equal("个人号", reloadedPersonal.AccountDisplayCache);
        Assert.Equal(15200, reloadedWork!.AmountCents);
        Assert.Equal("工作号", reloadedWork.AccountDisplayCache);
        Assert.Equal("银行卡/信用卡", reloadedWork.PaymentChannelName);
    }

    [Fact]
    public async Task AnchorDay31_EarlyActualPayment_DoesNotShiftMarchPlan()
    {
        var (_, ledger, _, clock) = CreateEnv(new DateTime(2026, 1, 31, 8, 0, 0));

        var sub = await ledger.SaveSubscriptionAsync(new SubscriptionDraft
        {
            AppName = "iCloud+",
            Category = BuiltInCategories.CloudStorage,
            AmountCents = 2100,
            CycleType = BillingCycleType.Monthly,
            NextBillingDate = new DateOnly(2026, 1, 31)
        });

        // 1 月 31 日当天自动生成待确认，NextBillingDate 推进到 2 月 28 日
        Assert.Equal(new DateOnly(2026, 2, 28), sub.NextBillingDate);

        // 跳到 2 月 28 日
        clock.NowLocal = new DateTime(2026, 2, 28, 10, 0, 0);
        await ledger.SyncDueOccurrencesAsync();

        var reloaded = await ledger.GetSubscriptionByIdAsync(sub.Id);
        Assert.Equal(new DateOnly(2026, 3, 31), reloaded!.NextBillingDate);

        var pendings = await ledger.GetPendingOccurrencesBySubscriptionAsync(sub.Id);
        var febOcc = pendings.Single(x => x.ScheduledDate == new DateOnly(2026, 2, 28));

        // 提前在 2 月 26 日实际扣款
        await ledger.ConfirmOccurrenceAsync(febOcc.Id, 2100, new DateOnly(2026, 2, 26));

        var afterConfirm = await ledger.GetSubscriptionByIdAsync(sub.Id);
        Assert.Equal(new DateOnly(2026, 3, 31), afterConfirm!.NextBillingDate);
    }

    [Fact]
    public async Task MissedTwoCycles_GeneratesTwoIndependentPending_AndConfirmIsIdempotent()
    {
        var (_, ledger, _, clock) = CreateEnv(new DateTime(2026, 9, 10, 9, 0, 0));

        var sub = await ledger.SaveSubscriptionAsync(new SubscriptionDraft
        {
            AppName = "网易云音乐",
            Category = BuiltInCategories.Media,
            AmountCents = 1500,
            CycleType = BillingCycleType.Monthly,
            NextBillingDate = new DateOnly(2026, 9, 15)
        });

        // 两个月后打开（漏过了 9/15 和 10/15）
        clock.NowLocal = new DateTime(2026, 11, 1, 10, 0, 0);
        var dashboard = await ledger.GetDashboardSummaryAsync();

        Assert.Equal(2, dashboard.PendingOccurrences.Count);
        Assert.Equal(new DateOnly(2026, 9, 15), dashboard.PendingOccurrences[0].Occurrence.ScheduledDate);
        Assert.Equal(new DateOnly(2026, 10, 15), dashboard.PendingOccurrences[1].Occurrence.ScheduledDate);

        // 重复确认第一期两次
        var firstOccId = dashboard.PendingOccurrences[0].Occurrence.Id;
        var rec1 = await ledger.ConfirmOccurrenceAsync(firstOccId, 1500, new DateOnly(2026, 9, 15));
        var rec2 = await ledger.ConfirmOccurrenceAsync(firstOccId, 1800, new DateOnly(2026, 9, 16));

        Assert.Equal(rec1.Id, rec2.Id);
        Assert.Equal(1500, rec2.PaidAmountCents);

        var records = await ledger.GetBillingRecordsBySubscriptionAsync(sub.Id);
        Assert.Single(records);

        var remaining = await ledger.GetPendingOccurrencesBySubscriptionAsync(sub.Id);
        Assert.Single(remaining);
        Assert.Equal(new DateOnly(2026, 10, 15), remaining[0].ScheduledDate);
    }

    [Fact]
    public async Task ConfirmAndSkip_UpdateUpcoming30DaysAndStatsAccurately()
    {
        var (_, ledger, _, clock) = CreateEnv(new DateTime(2026, 11, 10, 8, 0, 0));

        var subA = await ledger.SaveSubscriptionAsync(new SubscriptionDraft
        {
            AppName = "ChatGPT",
            Category = BuiltInCategories.AiTools,
            AmountCents = 14500,
            CycleType = BillingCycleType.Monthly,
            NextBillingDate = new DateOnly(2026, 11, 10)
        });

        var subB = await ledger.SaveSubscriptionAsync(new SubscriptionDraft
        {
            AppName = "Spotify",
            Category = BuiltInCategories.Media,
            AmountCents = 2800,
            CycleType = BillingCycleType.Monthly,
            NextBillingDate = new DateOnly(2026, 11, 10)
        });

        var before = await ledger.GetDashboardSummaryAsync();
        // 今天有两笔待确认，Hero 应展示金额最高的 ChatGPT，并标示同日 2 笔
        Assert.Equal("ChatGPT", before.HeroRenewal!.Subscription.AppName);
        Assert.Equal(2, before.HeroSameDayCount);
        Assert.Equal(0, before.HeroRenewal.DaysUntil);
        // 未来 30 天包含今天两笔待确认（下期 12/10 超出 11/10+29=12/9）
        Assert.Equal(14500 + 2800, before.Upcoming30DaysCents);

        var occA = before.PendingOccurrences.Single(x => x.Subscription.Id == subA.Id).Occurrence.Id;
        var occB = before.PendingOccurrences.Single(x => x.Subscription.Id == subB.Id).Occurrence.Id;

        await ledger.ConfirmOccurrenceAsync(occA, 14900, new DateOnly(2026, 11, 10));
        await ledger.SkipOccurrenceAsync(occB);

        var after = await ledger.GetDashboardSummaryAsync();
        Assert.Equal(0, after.Upcoming30DaysCents);
        Assert.Equal(14900, after.PaidThisMonthCents);
        Assert.Empty(after.PendingOccurrences);

        var stats = await ledger.GetStatisticsSummaryAsync(false, 2026, 11);
        Assert.Equal(14900, stats.TotalPaidCents);
    }

    [Fact]
    public async Task ManualRecord_AndOneTimePayment_WorkAsSpecified()
    {
        var (_, ledger, _, clock) = CreateEnv(new DateTime(2026, 11, 23, 10, 0, 0));

        // 限制 NextBillingDate >= Today
        await Assert.ThrowsAsync<ArgumentException>(() => ledger.SaveSubscriptionAsync(new SubscriptionDraft
        {
            AppName = "WPS",
            AmountCents = 2500,
            CycleType = BillingCycleType.Monthly,
            NextBillingDate = new DateOnly(2026, 11, 1)
        }));

        var wps = await ledger.SaveSubscriptionAsync(new SubscriptionDraft
        {
            AppName = "WPS",
            Category = BuiltInCategories.Productivity,
            AmountCents = 2500,
            CycleType = BillingCycleType.Monthly,
            NextBillingDate = new DateOnly(2026, 12, 10)
        });

        await ledger.AddManualBillingRecordAsync(wps.Id, 2500, new DateOnly(2026, 11, 10), "本月已扣补录");

        // 一次性项目勾选同时记入已付账单
        await ledger.SaveSubscriptionAsync(new SubscriptionDraft
        {
            AppName = "京东 PLUS",
            Category = BuiltInCategories.Lifestyle,
            AmountCents = 9900,
            CycleType = BillingCycleType.OneTime,
            NextBillingDate = new DateOnly(2027, 11, 23),
            RecordOneTimePayment = true,
            OneTimePaidAmountCents = 9900,
            OneTimePaidDate = new DateOnly(2026, 11, 23)
        });

        var dashboard = await ledger.GetDashboardSummaryAsync();
        Assert.Equal(2500 + 9900, dashboard.PaidThisMonthCents);
        Assert.Equal(2500, dashboard.Upcoming30DaysCents); // 仅 WPS 12/10，不含一次性项目
        Assert.Equal(2500, dashboard.MonthlyAverageCostCents); // 一次性不参与月均成本
    }

    [Fact]
    public async Task CancelResumeAndDeleteModes_KeepOrDeleteHistoryProperly()
    {
        var (_, ledger, _, clock) = CreateEnv(new DateTime(2026, 11, 1, 9, 0, 0));

        var sub = await ledger.SaveSubscriptionAsync(new SubscriptionDraft
        {
            AppName = "Claude",
            Category = BuiltInCategories.AiTools,
            AmountCents = 14500,
            CycleType = BillingCycleType.Monthly,
            NextBillingDate = new DateOnly(2026, 11, 1)
        });

        var occ = (await ledger.GetPendingOccurrencesBySubscriptionAsync(sub.Id)).Single();
        await ledger.ConfirmOccurrenceAsync(occ.Id, 14500, new DateOnly(2026, 11, 1));

        // 标记为不再续费并保留到期提醒
        await ledger.MarkAsCancelledAsync(sub.Id, new DateOnly(2026, 11, 30));
        var afterCancel = await ledger.GetDashboardSummaryAsync();
        Assert.Equal(0, afterCancel.Upcoming30DaysCents);
        Assert.Equal(0, afterCancel.MonthlyAverageCostCents);

        var remindersAfterCancel = await ledger.BuildUpcomingRemindersAsync();
        Assert.Contains(remindersAfterCancel, r => r.Kind == ReminderKind.ExpiryBefore || r.Kind == ReminderKind.ExpiryOnDay);

        // 恢复续费
        await ledger.ResumeSubscriptionAsync(sub.Id, new DateOnly(2026, 12, 1), 15000);
        var afterResume = await ledger.GetDashboardSummaryAsync();
        Assert.Equal(15000, afterResume.MonthlyAverageCostCents);

        // 删除订阅并保留历史账单
        await ledger.DeleteSubscriptionAsync(sub.Id, DeleteSubscriptionMode.KeepHistory);
        Assert.Null(await ledger.GetSubscriptionByIdAsync(sub.Id));
        var statsKeep = await ledger.GetStatisticsSummaryAsync(false, 2026, 11);
        Assert.Equal(14500, statsKeep.TotalPaidCents);
        Assert.True(statsKeep.Categories[0].Apps[0].HasDeletedRecords);

        // 再建一条并测试 DeleteHistory 模式
        var sub2 = await ledger.SaveSubscriptionAsync(new SubscriptionDraft
        {
            AppName = "Spotify",
            Category = BuiltInCategories.Media,
            AmountCents = 2800,
            CycleType = BillingCycleType.Monthly,
            NextBillingDate = new DateOnly(2026, 11, 1)
        });
        var occ2 = (await ledger.GetPendingOccurrencesBySubscriptionAsync(sub2.Id)).Single();
        await ledger.ConfirmOccurrenceAsync(occ2.Id, 2800, new DateOnly(2026, 11, 1));

        await ledger.DeleteSubscriptionAsync(sub2.Id, DeleteSubscriptionMode.DeleteHistory);
        var statsAfterDelete = await ledger.GetStatisticsSummaryAsync(false, 2026, 11);
        Assert.Equal(14500, statsAfterDelete.TotalPaidCents);
    }

    [Fact]
    public async Task BackupExportAndRestore_TransfersFullState_AndRejectsCorruptedJsonSafely()
    {
        var (_, ledger1, backup1, _) = CreateEnv(new DateTime(2026, 11, 10, 9, 0, 0));

        await ledger1.AddCustomPaymentChannelAsync("招商银行信用卡");
        var sub = await ledger1.SaveSubscriptionAsync(new SubscriptionDraft
        {
            AppName = "我的自建云盘",
            Category = BuiltInCategories.CloudStorage,
            TierName = "家庭版",
            AmountCents = 3900,
            CycleType = BillingCycleType.Monthly,
            NextBillingDate = new DateOnly(2026, 11, 10),
            AccountNickname = "家庭主号",
            PaymentChannelName = "招商银行信用卡"
        });
        var occ = (await ledger1.GetPendingOccurrencesBySubscriptionAsync(sub.Id)).Single();
        await ledger1.ConfirmOccurrenceAsync(occ.Id, 3900, new DateOnly(2026, 11, 10));

        var json = await backup1.ExportBackupJsonAsync();
        var preview = backup1.ValidateAndPreview(json);
        Assert.Equal(1, preview.SubscriptionCount);
        Assert.Equal(1, preview.RecordCount);

        // 恢复到一台全新的空白设备
        var (_, ledger2, backup2, _) = CreateEnv(new DateTime(2026, 11, 10, 12, 0, 0));
        await backup2.RestoreBackupJsonAsync(json);

        var subs2 = await ledger2.GetSubscriptionsAsync();
        Assert.Single(subs2);
        Assert.Equal("我的自建云盘", subs2[0].AppName);
        Assert.Equal("招商银行信用卡", subs2[0].PaymentChannelName);

        var channels2 = await ledger2.GetPaymentChannelsAsync();
        Assert.Contains(channels2, c => c.Name == "招商银行信用卡");

        var stats2 = await ledger2.GetStatisticsSummaryAsync(false, 2026, 11);
        Assert.Equal(3900, stats2.TotalPaidCents);

        // 尝试用损坏 JSON 恢复，应抛错且不破坏 ledger2 原数据
        await Assert.ThrowsAsync<InvalidDataException>(() => backup2.RestoreBackupJsonAsync("{ bad json"));
        Assert.Single(await ledger2.GetSubscriptionsAsync());
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }
        catch
        {
            // 忽略临时文件句柄清理警告
        }
    }
}
