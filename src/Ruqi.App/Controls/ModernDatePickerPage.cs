using Microsoft.Maui.Controls.Shapes;

namespace Ruqi.App.Controls;

/// <summary>
/// 现代冷白底部圆角日期选择器，彻底替代 Android 原生 DatePicker 弹窗。
/// </summary>
public sealed class ModernDatePickerPage : ContentPage
{
    private readonly TaskCompletionSource<DateOnly?> _tcs = new();
    private readonly DateOnly? _minDate;
    private readonly DateOnly? _maxDate;

    private readonly Label _monthYearLabel;
    private readonly Grid _daysGrid;
    private readonly Button _confirmBtn;

    private int _displayYear;
    private int _displayMonth;
    private DateOnly _selectedDate;
    private bool _closed;

    private ModernDatePickerPage(string title, DateOnly initialDate, DateOnly? minDate, DateOnly? maxDate)
    {
        _minDate = minDate;
        _maxDate = maxDate;

        _selectedDate = initialDate;
        if (_minDate.HasValue && _selectedDate < _minDate.Value) _selectedDate = _minDate.Value;
        if (_maxDate.HasValue && _selectedDate > _maxDate.Value) _selectedDate = _maxDate.Value;

        _displayYear = _selectedDate.Year;
        _displayMonth = _selectedDate.Month;

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

        // 顶部标题栏
        var header = new Grid
        {
            ColumnDefinitions =
            [
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            ],
            Padding = new Thickness(20, 4, 16, 8)
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

        // 快捷日期胶囊
        var quickRow = BuildQuickDateChips();

        // 年月切换栏
        _monthYearLabel = new Label
        {
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = UiHelper.TextPrimary,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };

        var navRow = new Grid
        {
            ColumnDefinitions =
            [
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto)
            ],
            ColumnSpacing = 6,
            Padding = new Thickness(16, 4)
        };
        navRow.Add(CreateNavBtn("«", () => ShiftYear(-1)), 0, 0);
        navRow.Add(CreateNavBtn("‹", () => ShiftMonth(-1)), 1, 0);
        navRow.Add(_monthYearLabel, 2, 0);
        navRow.Add(CreateNavBtn("›", () => ShiftMonth(1)), 3, 0);
        navRow.Add(CreateNavBtn("»", () => ShiftYear(1)), 4, 0);

        // 周一至周日表头
        var weekHeader = new Grid
        {
            Padding = new Thickness(14, 4, 14, 0),
            ColumnDefinitions = new ColumnDefinitionCollection(
                Enumerable.Range(0, 7).Select(_ => new ColumnDefinition(GridLength.Star)).ToArray())
        };
        string[] weeks = ["一", "二", "三", "四", "五", "六", "日"];
        for (var i = 0; i < 7; i++)
        {
            weekHeader.Add(new Label
            {
                Text = weeks[i],
                FontSize = 12,
                TextColor = UiHelper.TextMuted,
                HorizontalTextAlignment = TextAlignment.Center
            }, i, 0);
        }

        _daysGrid = new Grid
        {
            Padding = new Thickness(14, 4),
            RowSpacing = 4,
            ColumnSpacing = 2,
            ColumnDefinitions = new ColumnDefinitionCollection(
                Enumerable.Range(0, 7).Select(_ => new ColumnDefinition(GridLength.Star)).ToArray()),
            RowDefinitions = new RowDefinitionCollection(
                Enumerable.Range(0, 6).Select(_ => new RowDefinition(new GridLength(42))).ToArray())
        };

        // 底部清晰双按钮
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

        _confirmBtn = new Button
        {
            BackgroundColor = UiHelper.Primary,
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            FontSize = 15,
            CornerRadius = 14,
            HeightRequest = 46
        };
        _confirmBtn.Clicked += async (_, _) => await CloseAsync(_selectedDate);

        var footerGrid = new Grid
        {
            ColumnDefinitions =
            [
                new ColumnDefinition(new GridLength(1, GridUnitType.Star)),
                new ColumnDefinition(new GridLength(2, GridUnitType.Star))
            ],
            ColumnSpacing = 10,
            Padding = new Thickness(16, 8, 16, 16)
        };
        footerGrid.Add(cancelBtn, 0, 0);
        footerGrid.Add(_confirmBtn, 1, 0);

        var sheetContent = new VerticalStackLayout
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
                quickRow,
                navRow,
                weekHeader,
                _daysGrid,
                footerGrid
            }
        };

        var sheet = new Border
        {
            BackgroundColor = Colors.White,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(24, 24, 0, 0) },
            Content = sheetContent
        };

        root.Add(sheet, 0, 1);
        Content = root;

        RenderCalendar();
    }

    private View BuildQuickDateChips()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var candidates = new (string Label, DateOnly Date)[]
        {
            ("今天", today),
            ("下月今天", today.AddMonths(1)),
            ("半年后", today.AddMonths(6)),
            ("明年今天", today.AddYears(1))
        };

        var bar = new HorizontalStackLayout
        {
            Spacing = 8,
            Padding = new Thickness(16, 0, 16, 4)
        };

        foreach (var (label, dt) in candidates)
        {
            if (_minDate.HasValue && dt < _minDate.Value) continue;
            if (_maxDate.HasValue && dt > _maxDate.Value) continue;

            var chip = new Border
            {
                BackgroundColor = UiHelper.PrimarySoft,
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 12 },
                Padding = new Thickness(12, 6),
                Content = new Label
                {
                    Text = label,
                    FontSize = 12,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = UiHelper.Primary
                }
            };
            var captured = dt;
            UiHelper.AttachTap(chip, () =>
            {
                _selectedDate = captured;
                _displayYear = captured.Year;
                _displayMonth = captured.Month;
                RenderCalendar();
            });
            bar.Add(chip);
        }

        return new ScrollView
        {
            Orientation = ScrollOrientation.Horizontal,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Never,
            Content = bar
        };
    }

    private void ShiftMonth(int delta)
    {
        var dt = new DateOnly(_displayYear, _displayMonth, 1).AddMonths(delta);
        _displayYear = dt.Year;
        _displayMonth = dt.Month;
        RenderCalendar();
    }

    private void ShiftYear(int delta)
    {
        _displayYear = Math.Clamp(_displayYear + delta, 2000, 2100);
        RenderCalendar();
    }

    private void RenderCalendar()
    {
        _monthYearLabel.Text = $"{_displayYear}年{_displayMonth}月";
        _confirmBtn.Text = $"确定 · {_selectedDate:yyyy年M月d日}";
        _daysGrid.Children.Clear();

        var firstDay = new DateOnly(_displayYear, _displayMonth, 1);
        var daysInMonth = DateTime.DaysInMonth(_displayYear, _displayMonth);
        var startCol = ((int)firstDay.DayOfWeek + 6) % 7;
        var today = DateOnly.FromDateTime(DateTime.Today);

        for (var day = 1; day <= daysInMonth; day++)
        {
            var slot = startCol + (day - 1);
            var row = slot / 7;
            var col = slot % 7;
            var date = new DateOnly(_displayYear, _displayMonth, day);

            var isDisabled = (_minDate.HasValue && date < _minDate.Value) ||
                             (_maxDate.HasValue && date > _maxDate.Value);
            var isSelected = date == _selectedDate;
            var isToday = date == today;

            var cell = new Border
            {
                WidthRequest = 38,
                HeightRequest = 38,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                StrokeShape = new RoundRectangle { CornerRadius = 19 },
                StrokeThickness = isToday && !isSelected ? 1.2 : 0,
                Stroke = UiHelper.Primary,
                BackgroundColor = isSelected
                    ? UiHelper.Primary
                    : isToday ? UiHelper.PrimarySoft : Colors.Transparent,
                Content = new Label
                {
                    Text = day.ToString(),
                    FontSize = 14,
                    FontAttributes = isSelected || isToday ? FontAttributes.Bold : FontAttributes.None,
                    TextColor = isSelected
                        ? Colors.White
                        : isDisabled
                            ? Color.FromArgb("#CBD2E0")
                            : isToday ? UiHelper.Primary : UiHelper.TextPrimary,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                }
            };

            if (!isDisabled)
            {
                var captured = date;
                UiHelper.AttachTap(cell, () =>
                {
                    _selectedDate = captured;
                    RenderCalendar();
                });
            }

            _daysGrid.Add(cell, col, row);
        }
    }

    private static View CreateNavBtn(string glyph, Action onTap)
    {
        return UiHelper.CreateCircleIconButton(glyph, onTap, Color.FromArgb("#F4F6FB"), UiHelper.TextSecondary, 32);
    }

    protected override bool OnBackButtonPressed()
    {
        _ = CloseAsync(null);
        return true;
    }

    private async Task CloseAsync(DateOnly? result)
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

    public static async Task<DateOnly?> PickAsync(
        INavigation navigation,
        string title,
        DateOnly initialDate,
        DateOnly? minDate = null,
        DateOnly? maxDate = null)
    {
        var page = new ModernDatePickerPage(title, initialDate, minDate, maxDate);
        await navigation.PushModalAsync(page, animated: false);
        return await page._tcs.Task;
    }
}
