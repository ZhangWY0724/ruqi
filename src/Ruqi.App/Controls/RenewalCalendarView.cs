using Microsoft.Maui.Controls.Shapes;
using Ruqi.Core.Models;

namespace Ruqi.App.Controls;

/// <summary>
/// 轻量自定义续费月历控件：支持周一至周日排布、四色状态圆点（计划/待确认/已扣款/仅到期）及日期点选。
/// </summary>
public sealed class RenewalCalendarView : ContentView
{
    private readonly Grid _daysGrid;
    private readonly Label _monthTitleLabel;

    private int _year = DateTime.Today.Year;
    private int _month = DateTime.Today.Month;
    private DateOnly _selectedDate = DateOnly.FromDateTime(DateTime.Today);
    private IReadOnlyList<CalendarEventItem> _events = [];

    public event EventHandler<DateOnly>? SelectedDateChanged;
    public event EventHandler<(int Year, int Month)>? MonthChanged;

    public int Year => _year;
    public int Month => _month;
    public DateOnly SelectedDate => _selectedDate;

    public RenewalCalendarView()
    {
        var root = new VerticalStackLayout { Spacing = 10 };

        // 顶部月份切换栏
        var header = new Grid
        {
            ColumnDefinitions =
            [
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto)
            ],
            ColumnSpacing = 6,
            VerticalOptions = LayoutOptions.Center
        };

        var prevBtn = CreateMonthNavButton("‹", () => ShiftMonth(-1));
        var nextBtn = CreateMonthNavButton("›", () => ShiftMonth(1));

        _monthTitleLabel = new Label
        {
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#182033"),
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };

        var todayBtn = new Border
        {
            BackgroundColor = Color.FromArgb("#EEF0FF"),
            StrokeThickness = 0,
            Padding = new Thickness(10, 4),
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            VerticalOptions = LayoutOptions.Center,
            Content = new Label
            {
                Text = "今天",
                FontSize = 12,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#5B6CF9")
            }
        };
        var todayTap = new TapGestureRecognizer();
        todayTap.Tapped += (_, _) => JumpToToday();
        todayBtn.GestureRecognizers.Add(todayTap);

        header.Add(prevBtn, 0, 0);
        header.Add(_monthTitleLabel, 1, 0);
        header.Add(nextBtn, 2, 0);
        header.Add(todayBtn, 3, 0);

        // 四色图例
        var legend = new HorizontalStackLayout
        {
            Spacing = 14,
            HorizontalOptions = LayoutOptions.Center,
            Children =
            {
                CreateLegendItem("计划", "#5B6CF9"),
                CreateLegendItem("待确认", "#E89B2D"),
                CreateLegendItem("已扣款", "#2EA872"),
                CreateLegendItem("仅到期", "#8A94A6")
            }
        };

        // 星期表头（周一至周日）
        var weekHeader = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection(
                Enumerable.Repeat(new ColumnDefinition(GridLength.Star), 7).ToArray())
        };
        string[] weekNames = ["一", "二", "三", "四", "五", "六", "日"];
        for (var i = 0; i < 7; i++)
        {
            weekHeader.Add(new Label
            {
                Text = weekNames[i],
                FontSize = 12,
                TextColor = Color.FromArgb("#98A2B3"),
                HorizontalTextAlignment = TextAlignment.Center
            }, i, 0);
        }

        _daysGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection(
                Enumerable.Repeat(new ColumnDefinition(GridLength.Star), 7).ToArray()),
            RowDefinitions = new RowDefinitionCollection(
                Enumerable.Range(0, 6).Select(_ => new RowDefinition(new GridLength(46))).ToArray()),
            RowSpacing = 4,
            ColumnSpacing = 2
        };

        root.Add(header);
        root.Add(legend);
        root.Add(weekHeader);
        root.Add(_daysGrid);

        Content = new Border
        {
            BackgroundColor = Colors.White,
            Stroke = Color.FromArgb("#E8ECF5"),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 18 },
            Padding = new Thickness(14, 14),
            Content = root
        };

        Render();
    }

    public void SetMonthData(int year, int month, DateOnly selectedDate, IReadOnlyList<CalendarEventItem> events)
    {
        _year = year;
        _month = month;
        _selectedDate = selectedDate;
        _events = events;
        Render();
    }

    public void SelectDate(DateOnly date)
    {
        _selectedDate = date;
        Render();
        SelectedDateChanged?.Invoke(this, date);
    }

    private void ShiftMonth(int delta)
    {
        var dt = new DateOnly(_year, _month, 1).AddMonths(delta);
        _year = dt.Year;
        _month = dt.Month;
        _selectedDate = new DateOnly(_year, _month, 1);
        Render();
        MonthChanged?.Invoke(this, (_year, _month));
        SelectedDateChanged?.Invoke(this, _selectedDate);
    }

    private void JumpToToday()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var monthChanged = _year != today.Year || _month != today.Month;
        _year = today.Year;
        _month = today.Month;
        _selectedDate = today;
        Render();
        if (monthChanged)
        {
            MonthChanged?.Invoke(this, (_year, _month));
        }
        SelectedDateChanged?.Invoke(this, _selectedDate);
    }

    private void Render()
    {
        _monthTitleLabel.Text = $"{_year}年{_month}月";
        _daysGrid.Children.Clear();

        var firstDay = new DateOnly(_year, _month, 1);
        var daysInMonth = DateTime.DaysInMonth(_year, _month);
        // 周一=0 .. 周日=6
        var startCol = ((int)firstDay.DayOfWeek + 6) % 7;
        var today = DateOnly.FromDateTime(DateTime.Today);

        var eventsByDay = _events
            .Where(e => e.EventDate.Year == _year && e.EventDate.Month == _month)
            .GroupBy(e => e.EventDate.Day)
            .ToDictionary(g => g.Key, g => g.ToList());

        for (var day = 1; day <= daysInMonth; day++)
        {
            var slot = startCol + (day - 1);
            var row = slot / 7;
            var col = slot % 7;
            var date = new DateOnly(_year, _month, day);
            var isSelected = date == _selectedDate;
            var isToday = date == today;

            var cellStack = new VerticalStackLayout
            {
                Spacing = 2,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            };

            var circle = new Border
            {
                WidthRequest = 30,
                HeightRequest = 30,
                StrokeShape = new RoundRectangle { CornerRadius = 15 },
                StrokeThickness = isToday && !isSelected ? 1.2 : 0,
                Stroke = Color.FromArgb("#5B6CF9"),
                BackgroundColor = isSelected
                    ? Color.FromArgb("#5B6CF9")
                    : isToday ? Color.FromArgb("#EEF0FF") : Colors.Transparent,
                Content = new Label
                {
                    Text = day.ToString(),
                    FontSize = 13,
                    FontAttributes = isSelected || isToday ? FontAttributes.Bold : FontAttributes.None,
                    TextColor = isSelected
                        ? Colors.White
                        : isToday ? Color.FromArgb("#5B6CF9") : Color.FromArgb("#182033"),
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                }
            };

            var dots = new HorizontalStackLayout
            {
                Spacing = 3,
                HorizontalOptions = LayoutOptions.Center,
                HeightRequest = 6
            };

            if (eventsByDay.TryGetValue(day, out var dayEvents))
            {
                if (dayEvents.Any(e => e.Kind == OccurrenceKind.Renewal && e.Status == OccurrenceStatus.Planned))
                    dots.Add(CreateDot("#5B6CF9"));
                if (dayEvents.Any(e => e.Kind == OccurrenceKind.Renewal && e.Status == OccurrenceStatus.Pending))
                    dots.Add(CreateDot("#E89B2D"));
                if (dayEvents.Any(e => e.Status == OccurrenceStatus.Confirmed))
                    dots.Add(CreateDot("#2EA872"));
                if (dayEvents.Any(e => e.Kind == OccurrenceKind.ExpiryOnly))
                    dots.Add(CreateDot("#8A94A6"));
            }

            cellStack.Add(circle);
            cellStack.Add(dots);

            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) => SelectDate(date);
            cellStack.GestureRecognizers.Add(tap);

            _daysGrid.Add(cellStack, col, row);
        }
    }

    private static View CreateMonthNavButton(string text, Action onTap)
    {
        var kind = text == "‹" ? VectorIconKind.ChevronLeft : VectorIconKind.ChevronRight;
        return UiHelper.CreateCircleIconButton(kind, onTap, Color.FromArgb("#F4F6FB"), Color.FromArgb("#475467"), 32, 1.9f);
    }

    private static View CreateLegendItem(string label, string colorHex) => new HorizontalStackLayout
    {
        Spacing = 4,
        VerticalOptions = LayoutOptions.Center,
        Children =
        {
            CreateDot(colorHex),
            new Label
            {
                Text = label,
                FontSize = 11,
                TextColor = Color.FromArgb("#68738A"),
                VerticalOptions = LayoutOptions.Center
            }
        }
    };

    private static BoxView CreateDot(string colorHex) => new()
    {
        WidthRequest = 5,
        HeightRequest = 5,
        CornerRadius = 2.5,
        Color = Color.FromArgb(colorHex),
        VerticalOptions = LayoutOptions.Center
    };
}
