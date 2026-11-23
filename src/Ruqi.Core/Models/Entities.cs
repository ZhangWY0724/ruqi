namespace Ruqi.Core.Models;

public sealed class CatalogItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public bool IsBuiltIn { get; set; }
    public string AppName { get; set; } = string.Empty;
    public string Category { get; set; } = BuiltInCategories.Other;
    public string IconKey { get; set; } = string.Empty;
    public string BrandColor { get; set; } = "#5B6CF9";
    public string? TierName { get; set; }
    public BillingCycleType DefaultCycleType { get; set; } = BillingCycleType.Monthly;
    public int DefaultInterval { get; set; } = 1;
    public long? ReferencePriceCents { get; set; }
    public string SearchKeywords { get; set; } = string.Empty;
}

public sealed class AccountProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string AppName { get; set; } = string.Empty;
    public string? Nickname { get; set; }
    public string? Identifier { get; set; }
}

public sealed class PaymentChannel
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public bool IsBuiltIn { get; set; }
    public int SortOrder { get; set; }
    public bool IsArchived { get; set; }
}

public sealed class Subscription
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string AppName { get; set; } = string.Empty;
    public string Category { get; set; } = BuiltInCategories.Other;
    public string IconKey { get; set; } = string.Empty;
    public string BrandColor { get; set; } = "#5B6CF9";
    public string? TierName { get; set; }
    public long AmountCents { get; set; }
    public string Currency { get; set; } = "CNY";
    public BillingCycleType CycleType { get; set; } = BillingCycleType.Monthly;
    public int CycleInterval { get; set; } = 1;
    public int AnchorDay { get; set; } = 1;
    public int AnchorMonth { get; set; } = 1;
    public DateOnly NextBillingDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public string? AccountId { get; set; }
    public string? AccountDisplayCache { get; set; }
    public string? PaymentChannelName { get; set; }
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;
    public bool RemindBeforeEnabled { get; set; } = true;
    public int RemindBeforeDays { get; set; } = 3;
    public bool RemindOnDayEnabled { get; set; } = true;
    public TimeOnly RemindTime { get; set; } = new(9, 0);
    public string? Note { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class BillingOccurrence
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string SubscriptionId { get; set; } = string.Empty;
    public DateOnly ScheduledDate { get; set; }
    public long ExpectedAmountCents { get; set; }
    public OccurrenceKind Kind { get; set; } = OccurrenceKind.Renewal;
    public OccurrenceStatus Status { get; set; } = OccurrenceStatus.Planned;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class BillingRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string? OccurrenceId { get; set; }
    public string? SubscriptionId { get; set; }
    public long PaidAmountCents { get; set; }
    public string Currency { get; set; } = "CNY";
    public DateOnly PaidDate { get; set; }
    public string AppNameSnapshot { get; set; } = string.Empty;
    public string CategorySnapshot { get; set; } = BuiltInCategories.Other;
    public string IconKeySnapshot { get; set; } = string.Empty;
    public string BrandColorSnapshot { get; set; } = "#5B6CF9";
    public string? TierNameSnapshot { get; set; }
    public string? AccountDisplaySnapshot { get; set; }
    public string? PaymentChannelSnapshot { get; set; }
    public string? Note { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class AppSettings
{
    public bool DefaultRemindBeforeEnabled { get; set; } = true;
    public int DefaultRemindBeforeDays { get; set; } = 3;
    public bool DefaultRemindOnDayEnabled { get; set; } = true;
    public TimeOnly DefaultRemindTime { get; set; } = new(9, 0);
    public DateTimeOffset? LastBackupAt { get; set; }
}

public static class BuiltInCategories
{
    public const string AiTools = "AI 工具";
    public const string Media = "影音娱乐";
    public const string CloudStorage = "云存储";
    public const string Productivity = "办公效率";
    public const string Gaming = "游戏会员";
    public const string Lifestyle = "生活购物";
    public const string LifeServices = "生活服务";
    public const string Other = "其他";

    public static readonly IReadOnlyList<string> All =
    [
        AiTools,
        Media,
        CloudStorage,
        Productivity,
        Gaming,
        Lifestyle,
        LifeServices,
        Other
    ];
}
