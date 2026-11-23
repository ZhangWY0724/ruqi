using Microsoft.Maui.Controls.Shapes;

namespace Ruqi.App.Controls;

/// <summary>
/// 轻量现代分段切换控件（冷灰圆角槽 + 纯白悬浮滑块），彻底替代 SfSegmentedControl。
/// </summary>
public sealed class SegmentedSwitch : ContentView
{
    private readonly Grid _grid;
    private readonly List<Border> _items = [];
    private readonly List<Label> _labels = [];
    private int _selectedIndex;

    public event EventHandler<int>? SelectedIndexChanged;

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            if (_items.Count == 0)
            {
                _selectedIndex = Math.Max(0, value);
                return;
            }

            var clamped = Math.Clamp(value, 0, _items.Count - 1);
            if (_selectedIndex == clamped)
            {
                UpdateVisuals();
                return;
            }

            _selectedIndex = clamped;
            UpdateVisuals();
            SelectedIndexChanged?.Invoke(this, _selectedIndex);
        }
    }

    public SegmentedSwitch(params string[] titles)
    {
        _grid = new Grid
        {
            ColumnSpacing = 4,
            ColumnDefinitions = new ColumnDefinitionCollection(
                Enumerable.Range(0, titles.Length).Select(_ => new ColumnDefinition(GridLength.Star)).ToArray())
        };

        for (var i = 0; i < titles.Length; i++)
        {
            var idx = i;
            var label = new Label
            {
                Text = titles[i],
                FontSize = 14,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            };

            var pill = new Border
            {
                HeightRequest = 34,
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 10 },
                Content = label
            };

            UiHelper.AttachTap(pill, () => SelectedIndex = idx);

            _items.Add(pill);
            _labels.Add(label);
            _grid.Add(pill, i, 0);
        }

        Content = new Border
        {
            BackgroundColor = Color.FromArgb("#E9EDF7"),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 13 },
            Padding = new Thickness(4),
            Content = _grid
        };

        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        for (var i = 0; i < _items.Count; i++)
        {
            var selected = i == _selectedIndex;
            _items[i].BackgroundColor = selected ? Colors.White : Colors.Transparent;
            _items[i].Shadow = selected
                ? new Shadow { Brush = Colors.Black, Offset = new Point(0, 1), Radius = 4, Opacity = 0.06f }
                : null!;
            _labels[i].TextColor = selected ? UiHelper.Primary : UiHelper.TextSecondary;
            _labels[i].FontAttributes = selected ? FontAttributes.Bold : FontAttributes.None;
        }
    }
}
