using Microsoft.Maui.Controls.Shapes;
using Ruqi.Core.Models;

namespace Ruqi.App.Controls;

/// <summary>
/// 一屏直观可见的「默认提醒设置」底部抽屉，彻底替代逻辑割裂的多步菜单。
/// </summary>
public sealed class DefaultReminderSheetPage : ContentPage
{
    private readonly TaskCompletionSource<AppSettings?> _tcs = new();
    private readonly AppSettings _working;

    private readonly Switch _beforeSwitch;
    private readonly VerticalStackLayout _daysSection;
    private readonly HorizontalStackLayout _daysChipBar;
    private readonly Switch _onDaySwitch;
    private readonly Border _timeSectionCard;
    private readonly Label _timeValueLabel;

    private bool _closed;

    private DefaultReminderSheetPage(AppSettings initial)
    {
        _working = new AppSettings
        {
            DefaultRemindBeforeEnabled = initial.DefaultRemindBeforeEnabled,
            DefaultRemindBeforeDays = Math.Max(1, initial.DefaultRemindBeforeDays),
            DefaultRemindOnDayEnabled = initial.DefaultRemindOnDayEnabled,
            DefaultRemindTime = initial.DefaultRemindTime,
            LastBackupAt = initial.LastBackupAt
        };

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

        // 顶部标题
        var header = new Grid
        {
            ColumnDefinitions =
            [
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            ],
            Padding = new Thickness(20, 4, 16, 4)
        };
        header.Add(new VerticalStackLayout
        {
            Spacing = 2,
            Children =
            {
                new Label { Text = "默认提醒设置", FontSize = 18, FontAttributes = FontAttributes.Bold, TextColor = UiHelper.TextPrimary },
                new Label { Text = "用于新建订阅的默认选项，不影响已保存的订阅", FontSize = 12, TextColor = UiHelper.TextMuted }
            }
        }, 0, 0);

        var closeBtn = UiHelper.CreateCircleIconButton(VectorIconKind.Close, async () => await CloseAsync(null), Color.FromArgb("#F1F4FA"), UiHelper.TextSecondary, 32, 1.9f);
        header.Add(closeBtn, 1, 0);

        // 1. 续费前提前提醒
        _beforeSwitch = new Switch { IsToggled = _working.DefaultRemindBeforeEnabled, VerticalOptions = LayoutOptions.Center };
        _beforeSwitch.Toggled += (_, e) =>
        {
            _working.DefaultRemindBeforeEnabled = e.Value;
            UpdateVisibility();
        };

        _daysChipBar = new HorizontalStackLayout { Spacing = 8 };
        _daysSection = new VerticalStackLayout
        {
            Spacing = 8,
            Padding = new Thickness(0, 10, 0, 2),
            Children =
            {
                UiHelper.CreateDivider(),
                new Label { Text = "提前几天发送提醒", FontSize = 13, TextColor = UiHelper.TextSecondary },
                new ScrollView { Orientation = ScrollOrientation.Horizontal, HorizontalScrollBarVisibility = ScrollBarVisibility.Never, Content = _daysChipBar }
            }
        };

        var beforeCard = new Border
        {
            BackgroundColor = Color.FromArgb("#F7F9FC"),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            Padding = new Thickness(14, 12),
            Content = new VerticalStackLayout
            {
                Spacing = 0,
                Children =
                {
                    BuildToggleRow("续费前提前提醒", "扣款日前提前通知，留足取消或换卡时间", _beforeSwitch),
                    _daysSection
                }
            }
        };

        // 2. 续费当天提醒
        _onDaySwitch = new Switch { IsToggled = _working.DefaultRemindOnDayEnabled, VerticalOptions = LayoutOptions.Center };
        _onDaySwitch.Toggled += (_, e) =>
        {
            _working.DefaultRemindOnDayEnabled = e.Value;
            UpdateVisibility();
        };

        var onDayCard = new Border
        {
            BackgroundColor = Color.FromArgb("#F7F9FC"),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            Padding = new Thickness(14, 12),
            Content = BuildToggleRow("续费当天提醒", "扣费当天发送通知，方便及时确认记账", _onDaySwitch)
        };

        // 3. 提醒时间
        _timeValueLabel = new Label
        {
            Text = _working.DefaultRemindTime.ToString("HH:mm"),
            FontSize = 15,
            FontAttributes = FontAttributes.Bold,
            TextColor = UiHelper.Primary,
            VerticalOptions = LayoutOptions.Center
        };

        var timeRow = new Grid
        {
            ColumnDefinitions =
            [
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            ]
        };
        timeRow.Add(new VerticalStackLayout
        {
            Spacing = 2,
            Children =
            {
                new Label { Text = "默认提醒时间", FontSize = 15, FontAttributes = FontAttributes.Bold, TextColor = UiHelper.TextPrimary },
                new Label { Text = "点击选择通知弹出时刻", FontSize = 12, TextColor = UiHelper.TextMuted }
            }
        }, 0, 0);
        timeRow.Add(new Border
        {
            BackgroundColor = UiHelper.PrimarySoft,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 10 },
            Padding = new Thickness(12, 6),
            VerticalOptions = LayoutOptions.Center,
            Content = new HorizontalStackLayout
            {
                Spacing = 4,
                Children =
                {
                    _timeValueLabel,
                    new Label { Text = "›", FontSize = 16, TextColor = UiHelper.Primary, VerticalOptions = LayoutOptions.Center }
                }
            }
        }, 1, 0);

        _timeSectionCard = new Border
        {
            BackgroundColor = Color.FromArgb("#F7F9FC"),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            Padding = new Thickness(14, 12),
            Content = timeRow
        };
        UiHelper.AttachTapAsync(_timeSectionCard, async () =>
        {
            var picked = await ModernTimePickerPage.PickAsync(Navigation, "选择默认提醒时间", _working.DefaultRemindTime);
            if (picked.HasValue)
            {
                _working.DefaultRemindTime = picked.Value;
                _timeValueLabel.Text = picked.Value.ToString("HH:mm");
            }
        });

        var saveBtn = new Button
        {
            Text = "保存默认设置",
            BackgroundColor = UiHelper.Primary,
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            FontSize = 16,
            CornerRadius = 15,
            HeightRequest = 48,
            Margin = new Thickness(0, 4, 0, 0)
        };
        saveBtn.Clicked += async (_, _) => await CloseAsync(_working);

        var body = new VerticalStackLayout
        {
            Padding = new Thickness(16, 8, 16, 20),
            Spacing = 12,
            Children = { beforeCard, onDayCard, _timeSectionCard, saveBtn }
        };

        var sheet = new Border
        {
            BackgroundColor = Colors.White,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(24, 24, 0, 0) },
            Content = new VerticalStackLayout
            {
                Spacing = 6,
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
                    body
                }
            }
        };

        root.Add(sheet, 0, 1);
        Content = root;

        RenderDaysChips();
        UpdateVisibility();
    }

    private void RenderDaysChips()
    {
        _daysChipBar.Children.Clear();
        int[] options = [1, 2, 3, 5, 7, 14];
        foreach (var d in options)
        {
            var selected = _working.DefaultRemindBeforeDays == d;
            var chip = new Border
            {
                BackgroundColor = selected ? UiHelper.Primary : Colors.White,
                Stroke = selected ? UiHelper.Primary : UiHelper.CardBorder,
                StrokeThickness = 1,
                StrokeShape = new RoundRectangle { CornerRadius = 12 },
                Padding = new Thickness(14, 7),
                Content = new Label
                {
                    Text = $"{d}天",
                    FontSize = 13,
                    FontAttributes = selected ? FontAttributes.Bold : FontAttributes.None,
                    TextColor = selected ? Colors.White : UiHelper.TextPrimary
                }
            };
            var captured = d;
            UiHelper.AttachTap(chip, () =>
            {
                _working.DefaultRemindBeforeDays = captured;
                RenderDaysChips();
            });
            _daysChipBar.Add(chip);
        }
    }

    private void UpdateVisibility()
    {
        _daysSection.IsVisible = _working.DefaultRemindBeforeEnabled;
        _timeSectionCard.IsVisible = _working.DefaultRemindBeforeEnabled || _working.DefaultRemindOnDayEnabled;
    }

    private static View BuildToggleRow(string title, string subtitle, Switch sw)
    {
        var row = new Grid
        {
            ColumnDefinitions =
            [
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            ],
            ColumnSpacing = 10
        };
        row.Add(new VerticalStackLayout
        {
            Spacing = 2,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label { Text = title, FontSize = 15, FontAttributes = FontAttributes.Bold, TextColor = UiHelper.TextPrimary },
                new Label { Text = subtitle, FontSize = 12, TextColor = UiHelper.TextMuted }
            }
        }, 0, 0);
        row.Add(sw, 1, 0);
        return row;
    }

    protected override bool OnBackButtonPressed()
    {
        _ = CloseAsync(null);
        return true;
    }

    private async Task CloseAsync(AppSettings? result)
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

    public static async Task<AppSettings?> EditAsync(INavigation navigation, AppSettings current)
    {
        var page = new DefaultReminderSheetPage(current);
        await navigation.PushModalAsync(page, animated: false);
        return await page._tcs.Task;
    }
}
