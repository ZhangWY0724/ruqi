using Microsoft.Maui.Controls.Shapes;

namespace Ruqi.App.Controls;

public static class UiHelper
{
    public static Color Primary => Color.FromArgb("#5B6CF9");
    public static Color PrimarySoft => Color.FromArgb("#EEF0FF");
    public static Color PageBg => Color.FromArgb("#F4F6FB");
    public static Color CardBorder => Color.FromArgb("#E8ECF5");
    public static Color Divider => Color.FromArgb("#EDF0F7");
    public static Color TextPrimary => Color.FromArgb("#182033");
    public static Color TextSecondary => Color.FromArgb("#68738A");
    public static Color TextMuted => Color.FromArgb("#98A2B3");
    public static Color PendingColor => Color.FromArgb("#E89B2D");
    public static Color PendingBg => Color.FromArgb("#FFF5E5");
    public static Color ConfirmedColor => Color.FromArgb("#2EA872");
    public static Color ConfirmedBg => Color.FromArgb("#EAF8F1");
    public static Color DangerColor => Color.FromArgb("#E04F5F");
    public static Color DangerBg => Color.FromArgb("#FFF0F2");

    public static Border CreateCard(View content, Thickness? padding = null, double radius = 18) => new()
    {
        BackgroundColor = Colors.White,
        Stroke = CardBorder,
        StrokeThickness = 1,
        StrokeShape = new RoundRectangle { CornerRadius = radius },
        Padding = padding ?? new Thickness(16),
        Content = content
    };

    public static Border CreatePill(string text, Color bg, Color fg, double fontSize = 11) => new()
    {
        BackgroundColor = bg,
        StrokeThickness = 0,
        Padding = new Thickness(8, 2),
        StrokeShape = new RoundRectangle { CornerRadius = 8 },
        VerticalOptions = LayoutOptions.Center,
        HorizontalOptions = LayoutOptions.Start,
        Content = new Label
        {
            Text = text,
            FontSize = fontSize,
            FontAttributes = FontAttributes.Bold,
            TextColor = fg,
            VerticalOptions = LayoutOptions.Center
        }
    };

    public static Border CreateCircleIconButton(VectorIconKind iconKind, Action onTap, Color? bg = null, Color? fg = null, double size = 36, float stroke = 2.0f)
    {
        var iconSize = size * 0.48;
        var btn = new Border
        {
            WidthRequest = size,
            HeightRequest = size,
            BackgroundColor = bg ?? Colors.White,
            Stroke = CardBorder,
            StrokeThickness = bg is null ? 1 : 0,
            StrokeShape = new RoundRectangle { CornerRadius = size / 2 },
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center,
            Padding = 0,
            Content = new VectorIconView
            {
                Kind = iconKind,
                IconColor = fg ?? Primary,
                WidthRequest = iconSize,
                HeightRequest = iconSize,
                StrokeThickness = stroke,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            }
        };
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => onTap();
        btn.GestureRecognizers.Add(tap);
        return btn;
    }

    public static Border CreateCircleIconButton(string glyph, Action onTap, Color? bg = null, Color? fg = null, double size = 36)
    {
        var mapped = glyph switch
        {
            "+" => VectorIconKind.Plus,
            "⌕" => VectorIconKind.Search,
            "✕" => VectorIconKind.Close,
            "‹" => VectorIconKind.ChevronLeft,
            "›" => VectorIconKind.ChevronRight,
            "«" => VectorIconKind.DoubleChevronLeft,
            "»" => VectorIconKind.DoubleChevronRight,
            _ => (VectorIconKind?)null
        };

        if (mapped.HasValue)
        {
            return CreateCircleIconButton(mapped.Value, onTap, bg, fg, size);
        }

        var btn = new Border
        {
            WidthRequest = size,
            HeightRequest = size,
            BackgroundColor = bg ?? Colors.White,
            Stroke = CardBorder,
            StrokeThickness = bg is null ? 1 : 0,
            StrokeShape = new RoundRectangle { CornerRadius = size / 2 },
            VerticalOptions = LayoutOptions.Center,
            Content = new Label
            {
                Text = glyph,
                FontSize = 18,
                FontAttributes = FontAttributes.Bold,
                TextColor = fg ?? Primary,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            }
        };
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => onTap();
        btn.GestureRecognizers.Add(tap);
        return btn;
    }

    public static BoxView CreateDivider() => new()
    {
        HeightRequest = 1,
        Color = Divider,
        HorizontalOptions = LayoutOptions.Fill
    };

    public static void AttachTap(View view, Action action)
    {
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => action();
        view.GestureRecognizers.Add(tap);
    }

    public static void AttachTapAsync(View view, Func<Task> action)
    {
        var busy = false;
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) =>
        {
            if (busy) return;
            busy = true;
            try
            {
                await action();
            }
            finally
            {
                busy = false;
            }
        };
        view.GestureRecognizers.Add(tap);
    }

    /// <summary>
    /// 安全弹出指定模态页：仅当目标页位于当前模态栈顶时执行 PopModalAsync，
    /// 并留出极短缓冲等待原生导航事务彻底完成，避免连续弹窗时误弹新页或界面卡死。
    /// </summary>
    public static async Task PopModalSafelyAsync(Page modalPage)
    {
        try
        {
            var nav = modalPage.Navigation;
            var stack = nav?.ModalStack;
            if (stack is not null && stack.Count > 0 && ReferenceEquals(stack[^1], modalPage))
            {
                await nav!.PopModalAsync(animated: false);
            }
        }
        catch
        {
            // 忽略重复关闭或页面已卸载异常
        }

        await Task.Delay(35);
    }
}
