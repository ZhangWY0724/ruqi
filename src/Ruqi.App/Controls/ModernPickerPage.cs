using Microsoft.Maui.Controls.Shapes;

namespace Ruqi.App.Controls;

public sealed record PickerOption(string Value, string Title, string? Subtitle = null);

/// <summary>
/// 现代底部圆角选择抽屉，彻底取代老土的 Android 原生 Picker 下拉弹窗。
/// </summary>
public sealed class ModernPickerPage : ContentPage
{
    private readonly TaskCompletionSource<string?> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _closed;

    private ModernPickerPage(
        string title,
        IReadOnlyList<PickerOption> options,
        string? selectedValue,
        string? extraActionLabel)
    {
        BackgroundColor = Colors.Black.WithAlpha(0.38f);
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

        // 顶部把手 + 标题栏
        var header = new Grid
        {
            ColumnDefinitions =
            [
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            ],
            Padding = new Thickness(20, 4, 16, 12)
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

        var listStack = new VerticalStackLayout { Spacing = 8, Padding = new Thickness(16, 0, 16, 16) };

        foreach (var opt in options)
        {
            var isSelected = string.Equals(opt.Value, selectedValue, StringComparison.OrdinalIgnoreCase);

            var rowGrid = new Grid
            {
                ColumnDefinitions =
                [
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                ],
                ColumnSpacing = 8
            };

            var textStack = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center };
            textStack.Add(new Label
            {
                Text = opt.Title,
                FontSize = 15,
                FontAttributes = isSelected ? FontAttributes.Bold : FontAttributes.None,
                TextColor = isSelected ? UiHelper.Primary : UiHelper.TextPrimary
            });
            if (!string.IsNullOrWhiteSpace(opt.Subtitle))
            {
                textStack.Add(new Label
                {
                    Text = opt.Subtitle,
                    FontSize = 12,
                    TextColor = UiHelper.TextMuted
                });
            }

            rowGrid.Add(textStack, 0, 0);
            if (isSelected)
            {
                rowGrid.Add(new Label
                {
                    Text = "✓",
                    FontSize = 17,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = UiHelper.Primary,
                    VerticalOptions = LayoutOptions.Center
                }, 1, 0);
            }

            var itemCard = new Border
            {
                BackgroundColor = isSelected ? UiHelper.PrimarySoft : Color.FromArgb("#F7F9FC"),
                Stroke = isSelected ? Color.FromArgb("#D0D7FF") : Colors.Transparent,
                StrokeThickness = 1,
                StrokeShape = new RoundRectangle { CornerRadius = 14 },
                Padding = new Thickness(16, 13),
                Content = rowGrid
            };
            var val = opt.Value;
            UiHelper.AttachTapAsync(itemCard, () => CloseAsync(val));
            listStack.Add(itemCard);
        }

        if (!string.IsNullOrWhiteSpace(extraActionLabel))
        {
            var extraBtn = new Border
            {
                BackgroundColor = Colors.White,
                Stroke = Color.FromArgb("#DCE0FF"),
                StrokeThickness = 1,
                StrokeShape = new RoundRectangle { CornerRadius = 14 },
                Padding = new Thickness(16, 13),
                Content = new Label
                {
                    Text = extraActionLabel,
                    FontSize = 15,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = UiHelper.Primary,
                    HorizontalTextAlignment = TextAlignment.Center
                }
            };
            UiHelper.AttachTapAsync(extraBtn, () => CloseAsync("__EXTRA__"));
            listStack.Add(extraBtn);
        }

        var sheetContent = new VerticalStackLayout
        {
            Spacing = 4,
            Children =
            {
                new BoxView
                {
                    WidthRequest = 38,
                    HeightRequest = 4,
                    CornerRadius = 2,
                    Color = Color.FromArgb("#D5DCEB"),
                    HorizontalOptions = LayoutOptions.Center,
                    Margin = new Thickness(0, 10, 0, 6)
                },
                header,
                new ScrollView
                {
                    MaximumHeightRequest = 420,
                    Content = listStack
                }
            }
        };

        var sheet = new Border
        {
            BackgroundColor = Colors.White,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(24, 24, 0, 0) },
            Padding = new Thickness(0, 0, 0, 12),
            Content = sheetContent
        };

        root.Add(sheet, 0, 1);
        Content = root;
    }

    protected override bool OnBackButtonPressed()
    {
        _ = CloseAsync(null);
        return true;
    }

    private async Task CloseAsync(string? result)
    {
        if (_closed) return;
        _closed = true;
        await UiHelper.PopModalSafelyAsync(this);
        _tcs.TrySetResult(result);
    }

    public static async Task<string?> PickAsync(
        INavigation navigation,
        string title,
        IReadOnlyList<PickerOption> options,
        string? selectedValue = null,
        string? extraActionLabel = null)
    {
        var page = new ModernPickerPage(title, options, selectedValue, extraActionLabel);
        await navigation.PushModalAsync(page, animated: false);
        return await page._tcs.Task;
    }
}
