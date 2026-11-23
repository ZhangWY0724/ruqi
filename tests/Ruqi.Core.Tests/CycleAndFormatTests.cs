using Ruqi.Core.Models;
using Ruqi.Core.Services;

namespace Ruqi.Core.Tests;

public sealed class CycleAndFormatTests
{
    [Fact]
    public void MonthlyAnchor31_AcrossNonLeapYearFebruary_RestoresDay31()
    {
        var jan = new DateOnly(2026, 1, 31);
        var feb = CycleCalculator.GetNextDate(jan, BillingCycleType.Monthly, 1, 31, 1);
        var mar = CycleCalculator.GetNextDate(feb, BillingCycleType.Monthly, 1, 31, 1);
        var apr = CycleCalculator.GetNextDate(mar, BillingCycleType.Monthly, 1, 31, 1);
        var may = CycleCalculator.GetNextDate(apr, BillingCycleType.Monthly, 1, 31, 1);

        Assert.Equal(new DateOnly(2026, 2, 28), feb);
        Assert.Equal(new DateOnly(2026, 3, 31), mar);
        Assert.Equal(new DateOnly(2026, 4, 30), apr);
        Assert.Equal(new DateOnly(2026, 5, 31), may);
    }

    [Fact]
    public void YearlyAnchorFeb29_AcrossNonLeapAndLeapYears()
    {
        var y2024 = new DateOnly(2024, 2, 29);
        var y2025 = CycleCalculator.GetNextDate(y2024, BillingCycleType.Yearly, 1, 29, 2);
        var y2026 = CycleCalculator.GetNextDate(y2025, BillingCycleType.Yearly, 1, 29, 2);
        var y2027 = CycleCalculator.GetNextDate(y2026, BillingCycleType.Yearly, 1, 29, 2);
        var y2028 = CycleCalculator.GetNextDate(y2027, BillingCycleType.Yearly, 1, 29, 2);

        Assert.Equal(new DateOnly(2025, 2, 28), y2025);
        Assert.Equal(new DateOnly(2026, 2, 28), y2026);
        Assert.Equal(new DateOnly(2027, 2, 28), y2027);
        Assert.Equal(new DateOnly(2028, 2, 29), y2028);
    }

    [Fact]
    public void MonthlyCostEstimation_FollowsPrdFormulas()
    {
        Assert.Equal(14500, CycleCalculator.EstimateMonthlyCostCents(14500, BillingCycleType.Monthly, 1));
        Assert.Equal(2000, CycleCalculator.EstimateMonthlyCostCents(24000, BillingCycleType.Yearly, 1));
        Assert.Equal(3000, CycleCalculator.EstimateMonthlyCostCents(9000, BillingCycleType.EveryNMonths, 3));
        Assert.Equal(0, CycleCalculator.EstimateMonthlyCostCents(9900, BillingCycleType.OneTime, 1));

        // 10 元每 10 天：1000 * 365 / 12 / 10 = 3041.67 -> 3042 分
        Assert.Equal(3042, CycleCalculator.EstimateMonthlyCostCents(1000, BillingCycleType.EveryNDays, 10));
    }

    [Fact]
    public void FormatHelpers_ParseAndMaskWorkCorrectly()
    {
        Assert.True(FormatHelpers.TryParseCnyToCents("0", out var zero));
        Assert.Equal(0, zero);

        Assert.True(FormatHelpers.TryParseCnyToCents("145.5", out var c1));
        Assert.Equal(14550, c1);

        Assert.False(FormatHelpers.TryParseCnyToCents("-10", out _));
        Assert.False(FormatHelpers.TryParseCnyToCents("12.345", out _));

        Assert.Equal("wo***@outlook.com", FormatHelpers.MaskIdentifier("work@outlook.com"));
        Assert.Equal("138****8000", FormatHelpers.MaskIdentifier("13812348000"));
        Assert.Equal("工作号", FormatHelpers.BuildAccountDisplay("工作号", "work@outlook.com"));
        Assert.Equal("wo***@outlook.com", FormatHelpers.BuildAccountDisplay(null, "work@outlook.com"));
    }
}
