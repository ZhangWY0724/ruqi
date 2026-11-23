using Ruqi.Core.Data;
using Ruqi.Core.Services;

namespace Ruqi.App.Controls;

/// <summary>
/// 预设应用显示内置品牌图标，自定义应用显示带品牌底色的首字徽标。
/// </summary>
public sealed class AppIconBadge : ContentView
{
    private static readonly HashSet<string> BuiltInIconKeys = BuiltInSeeds.GetBuiltInCatalogItems()
        .Select(item => item.IconKey)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private readonly Image _brandIcon = new() { Aspect = Aspect.AspectFit };
    private readonly GraphicsView _initialBadge;

    public static readonly BindableProperty IconKeyProperty = BindableProperty.Create(
        nameof(IconKey), typeof(string), typeof(AppIconBadge), string.Empty,
        propertyChanged: (b, _, _) => ((AppIconBadge)b).RefreshIcon());

    public static readonly BindableProperty AppNameProperty = BindableProperty.Create(
        nameof(AppName), typeof(string), typeof(AppIconBadge), string.Empty,
        propertyChanged: (b, _, _) => ((AppIconBadge)b)._initialBadge.Invalidate());

    public static readonly BindableProperty BrandColorHexProperty = BindableProperty.Create(
        nameof(BrandColorHex), typeof(string), typeof(AppIconBadge), "#5B6CF9",
        propertyChanged: (b, _, _) => ((AppIconBadge)b)._initialBadge.Invalidate());

    public string IconKey
    {
        get => (string)GetValue(IconKeyProperty);
        set => SetValue(IconKeyProperty, value);
    }

    public string AppName
    {
        get => (string)GetValue(AppNameProperty);
        set => SetValue(AppNameProperty, value);
    }

    public string BrandColorHex
    {
        get => (string)GetValue(BrandColorHexProperty);
        set => SetValue(BrandColorHexProperty, value);
    }

    public AppIconBadge()
    {
        WidthRequest = 42;
        HeightRequest = 42;
        _initialBadge = new GraphicsView { Drawable = new BadgeDrawable(this) };
        Content = _initialBadge;
    }

    private void RefreshIcon()
    {
        var key = (IconKey ?? string.Empty).Trim().ToLowerInvariant();
        if (BuiltInIconKeys.Contains(key))
        {
            _brandIcon.Source = ImageSource.FromFile($"brand_{key}.png");
            Content = _brandIcon;
            return;
        }

        Content = _initialBadge;
        _initialBadge.Invalidate();
    }

    private sealed class BadgeDrawable(AppIconBadge owner) : IDrawable
    {
        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            var size = Math.Min(dirtyRect.Width, dirtyRect.Height);
            if (size <= 0) return;

            var x = dirtyRect.X + (dirtyRect.Width - size) / 2f;
            var y = dirtyRect.Y + (dirtyRect.Height - size) / 2f;
            var radius = size * 0.26f;

            var bgHex = string.IsNullOrWhiteSpace(owner.BrandColorHex)
                ? FormatHelpers.PickFallbackColor(owner.AppName)
                : owner.BrandColorHex;

            Color bgColor;
            try
            {
                bgColor = Color.FromArgb(bgHex);
            }
            catch
            {
                bgColor = Color.FromArgb("#5B6CF9");
            }

            canvas.SaveState();
            canvas.FillColor = bgColor;
            canvas.FillRoundedRectangle(x, y, size, size, radius);

            // 轻微高光内描边保持统一精致感
            canvas.StrokeColor = Colors.White.WithAlpha(0.18f);
            canvas.StrokeSize = 1f;
            canvas.DrawRoundedRectangle(x + 0.5f, y + 0.5f, size - 1f, size - 1f, radius);

            canvas.FontColor = Colors.White;
            canvas.FontSize = size * 0.44f;
            canvas.Font = Microsoft.Maui.Graphics.Font.DefaultBold;
            canvas.DrawString(FormatHelpers.GetAppInitial(owner.AppName), x, y, size, size,
                HorizontalAlignment.Center, VerticalAlignment.Center);
            canvas.RestoreState();
        }
    }
}
