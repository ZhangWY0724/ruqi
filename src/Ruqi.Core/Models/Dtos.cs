namespace Ruqi.Core.Models;

public sealed class SubscriptionDraft
{
    public string? Id { get; set; }
    public string AppName { get; set; } = string.Empty;
    public string Category { get; set; } = BuiltInCategories.Other;
    public string IconKey { get; set; } = string.Empty;
    public string BrandColor { get; set; } = "#5B6CF9";
    public string? TierName { get; set; }
    public long AmountCents { get; set; }
    public BillingCycleType CycleType { get; set; } = BillingCycleType.Monthly;
    public int CycleInterval { get; set; } = 1;
    public DateOnly NextBillingDate { get; set; }
    public string? AccountId { get; set; }
    public string? AccountNickname { get; set; }
    public string? AccountIdentifier { get; set; }
    public string? PaymentChannelName { get; set; }
    public bool RemindBeforeEnabled { get; set; } = true;
    public int RemindBeforeDays { get; set; } = 3;
    public bool RemindOnDayEnabled { get; set; } = true;
    public TimeOnly RemindTime { get; set; } = new(9, 0);
    public string? Note { get; set; }

    // 一次性到期项目新增时可选：同时记入一笔已付账单
    public bool RecordOneTimePayment { get; set; }
    public long? OneTimePaidAmountCents { get; set; }
    public DateOnly? OneTimePaidDate { get; set; }
}

public sealed class PendingOccurrenceView
{
    public required BillingOccurrence Occurrence { get; init; }
    public required Subscription Subscription { get; init; }
    public bool IsToday { get; init; }
    public int OverdueDays { get; init; }
}

public sealed class UpcomingRenewalView
{
    public required Subscription Subscription { get; init; }
    public DateOnly ScheduledDate { get; init; }
    public long ExpectedAmountCents { get; init; }
    public int DaysUntil { get; init; }
    public string? PendingOccurrenceId { get; init; }
}

public sealed class DashboardSummary
{
    public DateOnly Today { get; init; }
    public UpcomingRenewalView? HeroRenewal { get; init; }
    public int HeroSameDayCount { get; init; }
    public long PaidThisMonthCents { get; init; }
    public long Upcoming30DaysCents { get; init; }
    public long MonthlyAverageCostCents { get; init; }
    public List<PendingOccurrenceView> PendingOccurrences { get; init; } = [];
    public List<UpcomingRenewalView> UpcomingRenewals { get; init; } = [];
    public bool HasAnySubscription { get; init; }
}

public sealed class CalendarEventItem
{
    public string Id { get; init; } = string.Empty;
    public string? SubscriptionId { get; init; }
    public string? OccurrenceId { get; init; }
    public string? RecordId { get; init; }
    public DateOnly EventDate { get; init; }
    public OccurrenceKind Kind { get; init; }
    public OccurrenceStatus Status { get; init; }
    public string AppName { get; init; } = string.Empty;
    public string Category { get; init; } = BuiltInCategories.Other;
    public string IconKey { get; init; } = string.Empty;
    public string BrandColor { get; init; } = "#5B6CF9";
    public string? TierName { get; init; }
    public string? AccountDisplay { get; init; }
    public string? PaymentChannelName { get; init; }
    public long AmountCents { get; init; }
    public string CycleLabel { get; init; } = string.Empty;
}

public sealed class TrendBarPoint
{
    public string Label { get; init; } = string.Empty;
    public int Year { get; init; }
    public int Month { get; init; }
    public long AmountCents { get; init; }
    public double AmountYuan => AmountCents / 100.0;
    public bool IsHighlighted { get; init; }
}

public sealed class AccountStatItem
{
    public string AccountDisplay { get; init; } = "未指定账号";
    public long TotalCents { get; init; }
    public int RecordCount { get; init; }
    public bool IsDeletedSubscription { get; init; }
    public List<BillingRecord> Records { get; init; } = [];
}

public sealed class AppStatItem
{
    public string AppName { get; init; } = string.Empty;
    public string IconKey { get; init; } = string.Empty;
    public string BrandColor { get; init; } = "#5B6CF9";
    public long TotalCents { get; init; }
    public bool HasDeletedRecords { get; init; }
    public List<AccountStatItem> Accounts { get; init; } = [];
    public List<BillingRecord> Records { get; init; } = [];
}

public sealed class CategoryStatItem
{
    public string Category { get; init; } = string.Empty;
    public long TotalCents { get; init; }
    public double Ratio { get; init; }
    public List<AppStatItem> Apps { get; init; } = [];
}

public sealed class StatisticsSummary
{
    public bool IsYearly { get; init; }
    public int Year { get; init; }
    public int Month { get; init; }
    public long TotalPaidCents { get; init; }
    public long MonthlyAverageCostCents { get; init; }
    public List<TrendBarPoint> TrendPoints { get; init; } = [];
    public List<CategoryStatItem> Categories { get; init; } = [];
}

public sealed class ScheduledReminderItem
{
    public int NotificationId { get; init; }
    public string SubscriptionId { get; init; } = string.Empty;
    public string? OccurrenceId { get; init; }
    public ReminderKind Kind { get; init; }
    public DateOnly TargetDate { get; init; }
    public DateTime FireAtLocal { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
}

public sealed class BackupDocument
{
    public int SchemaVersion { get; set; } = 1;
    public string AppName { get; set; } = "如期";
    public DateTimeOffset ExportedAt { get; set; } = DateTimeOffset.UtcNow;
    public AppSettings Settings { get; set; } = new();
    public List<CatalogItem> CustomCatalogItems { get; set; } = [];
    public List<AccountProfile> Accounts { get; set; } = [];
    public List<PaymentChannel> CustomPaymentChannels { get; set; } = [];
    public List<Subscription> Subscriptions { get; set; } = [];
    public List<BillingOccurrence> Occurrences { get; set; } = [];
    public List<BillingRecord> Records { get; set; } = [];
}

public sealed class BackupPreview
{
    public int SchemaVersion { get; init; }
    public DateTimeOffset ExportedAt { get; init; }
    public int SubscriptionCount { get; init; }
    public int ActiveSubscriptionCount { get; init; }
    public int PendingOccurrenceCount { get; init; }
    public int RecordCount { get; init; }
    public int CustomItemCount { get; init; }
}
