using Ruqi.Core.Models;

namespace Ruqi.Core.Services;

public static class CycleCalculator
{
    public static DateOnly GetNextDate(
        DateOnly current,
        BillingCycleType cycleType,
        int interval,
        int anchorDay,
        int anchorMonth)
    {
        var safeInterval = Math.Max(1, interval);
        var safeAnchorDay = Math.Clamp(anchorDay, 1, 31);
        var safeAnchorMonth = Math.Clamp(anchorMonth, 1, 12);

        return cycleType switch
        {
            BillingCycleType.Monthly => AddMonthsWithAnchor(current, 1, safeAnchorDay),
            BillingCycleType.EveryNMonths => AddMonthsWithAnchor(current, safeInterval, safeAnchorDay),
            BillingCycleType.Yearly => AddYearsWithAnchor(current, 1, safeAnchorMonth, safeAnchorDay),
            BillingCycleType.EveryNDays => current.AddDays(safeInterval),
            BillingCycleType.OneTime => current,
            _ => AddMonthsWithAnchor(current, 1, safeAnchorDay)
        };
    }

    public static IEnumerable<DateOnly> EnumerateFutureScheduledDates(
        Subscription subscription,
        DateOnly rangeStartInclusive,
        DateOnly rangeEndInclusive)
    {
        if (subscription.Status != SubscriptionStatus.Active || subscription.CycleType == BillingCycleType.OneTime)
        {
            yield break;
        }

        if (rangeEndInclusive < rangeStartInclusive)
        {
            yield break;
        }

        var cursor = subscription.NextBillingDate;
        var guard = 0;
        const int maxIterations = 2000;

        while (cursor <= rangeEndInclusive && guard < maxIterations)
        {
            if (cursor >= rangeStartInclusive)
            {
                yield return cursor;
            }

            var next = GetNextDate(
                cursor,
                subscription.CycleType,
                subscription.CycleInterval,
                subscription.AnchorDay,
                subscription.AnchorMonth);

            if (next <= cursor)
            {
                yield break;
            }

            cursor = next;
            guard++;
        }
    }

    public static long EstimateMonthlyCostCents(Subscription subscription)
    {
        if (subscription.Status != SubscriptionStatus.Active || subscription.CycleType == BillingCycleType.OneTime)
        {
            return 0;
        }

        return EstimateMonthlyCostCents(subscription.AmountCents, subscription.CycleType, subscription.CycleInterval);
    }

    public static long EstimateMonthlyCostCents(long amountCents, BillingCycleType cycleType, int interval)
    {
        if (amountCents <= 0 || cycleType == BillingCycleType.OneTime)
        {
            return 0;
        }

        var safeInterval = Math.Max(1, interval);
        decimal monthly = cycleType switch
        {
            BillingCycleType.Monthly => amountCents,
            BillingCycleType.Yearly => amountCents / 12m,
            BillingCycleType.EveryNMonths => amountCents / (decimal)safeInterval,
            BillingCycleType.EveryNDays => amountCents * 365m / 12m / safeInterval,
            _ => 0m
        };

        return (long)decimal.Round(monthly, 0, MidpointRounding.AwayFromZero);
    }

    private static DateOnly AddMonthsWithAnchor(DateOnly current, int monthsToAdd, int anchorDay)
    {
        var totalMonths = current.Year * 12 + (current.Month - 1) + monthsToAdd;
        var targetYear = totalMonths / 12;
        var targetMonth = totalMonths % 12 + 1;
        var daysInMonth = DateTime.DaysInMonth(targetYear, targetMonth);
        var targetDay = Math.Min(anchorDay, daysInMonth);
        return new DateOnly(targetYear, targetMonth, targetDay);
    }

    private static DateOnly AddYearsWithAnchor(DateOnly current, int yearsToAdd, int anchorMonth, int anchorDay)
    {
        var targetYear = current.Year + yearsToAdd;
        var daysInMonth = DateTime.DaysInMonth(targetYear, anchorMonth);
        var targetDay = Math.Min(anchorDay, daysInMonth);
        return new DateOnly(targetYear, anchorMonth, targetDay);
    }
}
