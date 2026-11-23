using Microsoft.Maui.Controls.Shapes;

namespace Ruqi.App.Controls;

/// <summary>
/// 现代冷白底部圆角时间选择器（常用快捷时间 + 小时/分钟胶囊点选）。
/// </summary>
public sealed class ModernTimePickerPage : ContentPage
{
    private readonly TaskCompletionSource<TimeOnly?> _tcs = new();
    private readonly Label _previewLabel;
    private readonly HorizontalStackLayout _hourBar;
    private readonly HorizontalStackLayout _minuteBar;

    private int _hour;
    private int _minute;
    private bool _closed;

    private ModernTimePickerPage(string title, TimeOnly initialTime)
    {
        _hour = initialTime.Hour;
        _minute = initialTime.Minute;

        BackgroundColor = Colors.Black.WithAlpha(0.40f);
        Shell.SetNavBarIsVisible(this, false);

        var root = new Grid
        {
            RowDefinitions =
            [
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto)
            ]
        };

        var backdrop = new BoxView { Color = Colors.Transparent };
        UiHelper.AttachTapAsync(backdrop, () => CloseAsync(null));
        root.Add(backdrop, 0, 0);

        var header = new Grid
        {
            ColumnDefinitions =
            [
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            ],
            Padding = new Thickness(20, 4, 16, 4)
        };
        header.Add(new Label
        {
            Text = title,
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = UiHelper.TextPrimary,
            VerticalOptions = LayoutOptions.Center
        }, 0, 0);

        var closeBtn = UiHelper.CreateCircleIconButton(VectorIconKind.Close, async () => await CloseAsync(null), Color.FromArgb("#F1F4FA"), UiHelper.TextSecondary, 32, 1.9f);
        header.Add(closeBtn, 1, 0);

        _previewLabel = new Label
        {
            FontSize = 34,
            FontAttributes = FontAttributes.Bold,
            TextColor = UiHelper.Primary,
            HorizontalTextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 4, 0, 8)
        };

        // 常用时间一键快捷选
        var presets = new (int H, int M)[] { (8, 0), (9, 0), (10, 0), (12, 0), (18, 0), (20, 0) };
        var presetBar = new HorizontalStackLayout { Spacing = 8, Padding = new Thickness(16, 0) };
        foreach (var (h, m) in presets)
        {
            var chip = new Border
            {
                BackgroundColor = UiHelper.PrimarySoft,
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 12 },
                Padding = new Thickness(12, 6),
                Content = new Label
                {
                    Text = $"{h:00}:{m:00}",
                    FontSize = 13,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = UiHelper.Primary
                }
            };
            var ch = h;
            var cm = m;
            UiHelper.AttachTap(chip, () =>
            {
                _hour = ch;
                _minute = cm;
                RenderSelection();
            });
            presetBar.Add(chip);
        }

        _hourBar = new HorizontalStackLayout { Spacing = 8, Padding = new Thickness(16, 0) };
        _minuteBar = new HorizontalStackLayout { Spacing = 8, Padding = new Thickness(16, 0) };

        var cancelBtn = new Button
        {
            Text = "取消",
            BackgroundColor = Color.FromArgb("#F1F4FA"),
            TextColor = UiHelper.TextSecondary,
            FontAttributes = FontAttributes.Bold,
            FontSize = 15,
            CornerRadius = 14,
            HeightRequest = 46
        };
        cancelBtn.Clicked += async (_, _) => await CloseAsync(null);

        var confirmBtn = new Button
        {
            Text = "确定时间",
            BackgroundColor = UiHelper.Primary,
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            FontSize = 15,
            CornerRadius = 14,
            HeightRequest = 46
        };
        confirmBtn.Clicked += async (_, _) => await CloseAsync(new TimeOnly(_hour, _minute));

        var footer = new Grid
        {
            ColumnDefinitions =
            [
                new ColumnDefinition(new GridLength(1, GridUnitType.Star)),
                new ColumnDefinition(new GridLength(2, GridUnitType.Star))
            ],
            ColumnSpacing = 10,
            Padding = new Thickness(16, 12, 16, 18)
        };
        footer.Add(cancelBtn, 0, 0);
        footer.Add(confirmBtn, 1, 0);

        var sheet = new Border
        {
            BackgroundColor = Colors.White,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(24, 24, 0, 0) },
            Content = new VerticalStackLayout
            {
                Spacing = 8,
                Children =
                {
                    new BoxView
                    {
                        WidthRequest = 38,
                        HeightRequest = 4,
                        CornerRadius = 2,
                        Color = Color.FromArgb("#D5DCEB"),
                        HorizontalOptions = LayoutOptions.Center,
                        Margin = new Thickness(0, 10, 0, 4)
                    },
                    header,
                    _previewLabel,
                    new Label { Text = "常用时间", FontSize = 12, TextColor = UiHelper.TextMuted, Margin = new Thickness(18, 0, 0, 0) },
                    new ScrollView { Orientation = ScrollOrientation.Horizontal, HorizontalScrollBarVisibility = ScrollBarVisibility.Never, Content = presetBar },
                    new Label { Text = "小时", FontSize = 12, TextColor = UiHelper.TextMuted, Margin = new Thickness(18, 4, 0, 0) },
                    new ScrollView { Orientation = ScrollOrientation.Horizontal, HorizontalScrollBarVisibility = ScrollBarVisibility.Never, Content = _hourBar },
                    new Label { Text = "分钟", FontSize = 12, TextColor = UiHelper.TextMuted, Margin = new Thickness(18, 4, 0, 0) },
                    new ScrollView { Orientation = ScrollOrientation.Horizontal, HorizontalScrollBarVisibility = ScrollBarVisibility.Never, Content = _minuteBar },
                    footer
                }
            }
        };

        root.Add(sheet, 0, 1);
        Content = root;

        RenderSelection();
    }

    private void RenderSelection()
    {
        _previewLabel.Text = $"{_hour:00}:{_minute:00}";

        _hourBar.Children.Clear();
        for (var h = 0; h < 24; h++)
        {
            var isSelected = h == _hour;
            var ch = h;
            _hourBar.Add(CreatePill($"{h:00}时", isSelected, () =>
            {
                _hour = ch;
                RenderSelection();
            }));
        }

        _minuteBar.Children.Clear();
        for (var m = 0; m < 60; m += 5)
        {
            var isSelected = m == _minute;
            var cm = m;
            _minuteBar.Add(CreatePill($"{m:00}分", isSelected, () =>
            {
                _minute = cm;
                RenderSelection();
            }));
        }
    }

    private static View CreatePill(string text, bool selected, Action onTap)
    {
        var border = new Border
        {
            BackgroundColor = selected ? UiHelper.Primary : Color.FromArgb("#F2F4FA"),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Padding = new Thickness(14, 9),
            Content = new Label
            {
                Text = text,
                FontSize = 14,
                FontAttributes = selected ? FontAttributes.Bold : FontAttributes.None,
                TextColor = selected ? Colors.White : UiHelper.TextPrimary
            }
        };
        UiHelper.AttachTap(border, onTap);
        return border;
    }

    protected override bool OnBackButtonPressed()
    {
        _ = CloseAsync(null);
        return true;
    }

    private async Task CloseAsync(TimeOnly? result)
    {
        if (_closed) return;
        _closed = true;
        _tcs.TrySetResult(result);
        try
        {
            await Navigation.PopModalAsync(animated: false);
        }
        catch
        {
            // 忽略重复关闭
        }
    }

    public static async Task<TimeOnly?> PickAsync(INavigation navigation, string title, TimeOnly initialTime)
    {
        var page = new ModernTimePickerPage(title, initialTime);
        await navigation.PushModalAsync(page, animated: false);
        return await page._tcs.Task;
    }
}
