namespace Ruqi.Core.Models;

public enum BillingCycleType
{
    Monthly = 0,
    Yearly = 1,
    EveryNDays = 2,
    EveryNMonths = 3,
    OneTime = 4
}

public enum SubscriptionStatus
{
    Active = 0,
    Cancelled = 1
}

public enum OccurrenceKind
{
    Renewal = 0,
    ExpiryOnly = 1
}

public enum OccurrenceStatus
{
    Planned = 0,
    Pending = 1,
    Confirmed = 2,
    Skipped = 3
}

public enum DeleteSubscriptionMode
{
    KeepHistory = 0,
    DeleteHistory = 1
}

public enum ReminderKind
{
    RenewalBefore = 0,
    RenewalOnDay = 1,
    ExpiryBefore = 2,
    ExpiryOnDay = 3
}
