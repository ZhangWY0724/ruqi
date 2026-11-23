using System.Globalization;
using Ruqi.Core.Models;

namespace Ruqi.Core.Services;

public static class FormatHelpers
{
    public static string FormatCny(long cents, bool trimZeroDecimals = false)
    {
        var yuan = cents / 100m;
        if (trimZeroDecimals && cents % 100 == 0)
        {
            return $"¥{yuan:0}";
        }

        return $"¥{yuan:0.00}";
    }

    public static bool TryParseCnyToCents(string? input, out long cents)
    {
        cents = 0;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var cleaned = input.Trim().Replace("¥", string.Empty).Replace("￥", string.Empty).Replace(",", string.Empty);
        if (!decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
        {
            return false;
        }

        if (value < 0)
        {
            return false;
        }

        // 校验最多两位小数
        if (decimal.Round(value, 2, MidpointRounding.AwayFromZero) != value)
        {
            return false;
        }

        cents = (long)decimal.Round(value * 100m, 0, MidpointRounding.AwayFromZero);
        return true;
    }

    public static string MaskIdentifier(string? identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return string.Empty;
        }

        var trimmed = identifier.Trim();
        var atIndex = trimmed.IndexOf('@');
        if (atIndex > 0 && atIndex < trimmed.Length - 1)
        {
            var local = trimmed[..atIndex];
            var domain = trimmed[atIndex..];
            var prefix = local.Length switch
            {
                <= 1 => local,
                2 => local[..1],
                _ => local[..2]
            };
            return $"{prefix}***{domain}";
        }

        // 手机号或纯数字
        if (trimmed.Length == 11 && trimmed.All(char.IsDigit))
        {
            return $"{trimmed[..3]}****{trimmed[^4..]}";
        }

        if (trimmed.Length <= 2)
        {
            return $"{trimmed[0]}*";
        }

        if (trimmed.Length <= 5)
        {
            return $"{trimmed[..1]}***{trimmed[^1..]}";
        }

        return $"{trimmed[..2]}***{trimmed[^2..]}";
    }

    public static string? BuildAccountDisplay(string? nickname, string? identifier)
    {
        var nick = nickname?.Trim();
        if (!string.IsNullOrEmpty(nick))
        {
            return nick;
        }

        var masked = MaskIdentifier(identifier);
        return string.IsNullOrEmpty(masked) ? null : masked;
    }

    public static string BuildAccountDetailLine(string? nickname, string? identifier)
    {
        var nick = nickname?.Trim();
        var masked = MaskIdentifier(identifier);
        if (!string.IsNullOrEmpty(nick) && !string.IsNullOrEmpty(masked))
        {
            return $"{nick} · {masked}";
        }

        return !string.IsNullOrEmpty(nick) ? nick : masked;
    }

    public static string FormatCycleName(BillingCycleType type, int interval) => type switch
    {
        BillingCycleType.Monthly => "每月",
        BillingCycleType.Yearly => "每年",
        BillingCycleType.EveryNDays => $"每 {Math.Max(1, interval)} 天",
        BillingCycleType.EveryNMonths => $"每 {Math.Max(1, interval)} 个月",
        BillingCycleType.OneTime => "一次性到期",
        _ => "每月"
    };

    public static string FormatCycleUnitSuffix(BillingCycleType type, int interval) => type switch
    {
        BillingCycleType.Monthly => "/月",
        BillingCycleType.Yearly => "/年",
        BillingCycleType.EveryNDays => $"/{Math.Max(1, interval)}天",
        BillingCycleType.EveryNMonths => $"/{Math.Max(1, interval)}个月",
        BillingCycleType.OneTime => " 一次性",
        _ => "/月"
    };

    public static string FormatAmountWithCycle(long cents, BillingCycleType type, int interval)
        => $"{FormatCny(cents)}{FormatCycleUnitSuffix(type, interval)}";

    public static string FormatMonthDay(DateOnly date) => $"{date.Month}月{date.Day}日";

    public static string FormatFullDate(DateOnly date) => $"{date.Year}年{date.Month}月{date.Day}日";

    public static string FormatReminderSummary(Subscription sub)
    {
        if (!sub.RemindBeforeEnabled && !sub.RemindOnDayEnabled)
        {
            return "已关闭提醒";
        }

        var parts = new List<string>();
        if (sub.RemindBeforeEnabled)
        {
            parts.Add($"提前{Math.Max(1, sub.RemindBeforeDays)}天");
        }

        if (sub.RemindOnDayEnabled)
        {
            parts.Add("当天");
        }

        return $"{string.Join("、", parts)} · {sub.RemindTime:HH:mm}";
    }

    public static string GetAppInitial(string? appName)
    {
        if (string.IsNullOrWhiteSpace(appName))
        {
            return "订";
        }

        var trimmed = appName.Trim();
        var ch = trimmed[0];
        return char.IsLetter(ch) ? char.ToUpperInvariant(ch).ToString() : trimmed[..1];
    }

    public static string PickFallbackColor(string? appName)
    {
        string[] palette =
        [
            "#5B6CF9",
            "#4F86F7",
            "#2FA4A9",
            "#3B9E70",
            "#E08E45",
            "#D95F76",
            "#8A63D2",
            "#52799B"
        ];

        if (string.IsNullOrWhiteSpace(appName))
        {
            return palette[0];
        }

        var hash = 17;
        foreach (var ch in appName.Trim())
        {
            hash = unchecked(hash * 31 + ch);
        }

        return palette[Math.Abs(hash) % palette.Length];
    }
}
