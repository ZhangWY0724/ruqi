namespace Ruqi.App.Controls;

public enum VectorIconKind
{
    Plus,
    Search,
    Close,
    ChevronLeft,
    ChevronRight,
    DoubleChevronLeft,
    DoubleChevronRight,
    Trash
}

/// <summary>
/// 纯矢量几何图标控件：严格围绕画布几何中心绘制，彻底避免 Android 字体基线导致的视觉偏下问题。
/// </summary>
public sealed class VectorIconView : GraphicsView
{
    public static readonly BindableProperty KindProperty = BindableProperty.Create(
        nameof(Kind), typeof(VectorIconKind), typeof(VectorIconView), VectorIconKind.Plus,
        propertyChanged: (b, _, _) => ((VectorIconView)b).Invalidate());

    public static readonly BindableProperty IconColorProperty = BindableProperty.Create(
        nameof(IconColor), typeof(Color), typeof(VectorIconView), Color.FromArgb("#5B6CF9"),
        propertyChanged: (b, _, _) => ((VectorIconView)b).Invalidate());

    public static readonly BindableProperty StrokeThicknessProperty = BindableProperty.Create(
        nameof(StrokeThickness), typeof(float), typeof(VectorIconView), 2.0f,
        propertyChanged: (b, _, _) => ((VectorIconView)b).Invalidate());

    public VectorIconKind Kind
    {
        get => (VectorIconKind)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public Color IconColor
    {
        get => (Color)GetValue(IconColorProperty);
        set => SetValue(IconColorProperty, value);
    }

    public float StrokeThickness
    {
        get => (float)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public VectorIconView()
    {
        WidthRequest = 18;
        HeightRequest = 18;
        HorizontalOptions = LayoutOptions.Center;
        VerticalOptions = LayoutOptions.Center;
        InputTransparent = true;
        Drawable = new IconDrawable(this);
    }

    private sealed class IconDrawable(VectorIconView owner) : IDrawable
    {
        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            var s = Math.Min(dirtyRect.Width, dirtyRect.Height);
            if (s <= 2) return;

            var cx = dirtyRect.Center.X;
            var cy = dirtyRect.Center.Y;

            canvas.SaveState();
            canvas.StrokeColor = owner.IconColor;
            canvas.StrokeSize = owner.StrokeThickness;
            canvas.StrokeLineCap = LineCap.Round;
            canvas.StrokeLineJoin = LineJoin.Round;

            switch (owner.Kind)
            {
                case VectorIconKind.Plus:
                {
                    var arm = s * 0.34f;
                    canvas.DrawLine(cx - arm, cy, cx + arm, cy);
                    canvas.DrawLine(cx, cy - arm, cx, cy + arm);
                    break;
                }

                case VectorIconKind.Close:
                {
                    var arm = s * 0.27f;
                    canvas.DrawLine(cx - arm, cy - arm, cx + arm, cy + arm);
                    canvas.DrawLine(cx + arm, cy - arm, cx - arm, cy + arm);
                    break;
                }

                case VectorIconKind.Search:
                {
                    var r = s * 0.25f;
                    var ocx = cx - s * 0.08f;
                    var ocy = cy - s * 0.08f;
                    canvas.DrawCircle(ocx, ocy, r);
                    var startOffset = r * 0.72f;
                    var endX = cx + s * 0.34f;
                    var endY = cy + s * 0.34f;
                    canvas.DrawLine(ocx + startOffset, ocy + startOffset, endX, endY);
                    break;
                }

                case VectorIconKind.ChevronLeft:
                {
                    var w = s * 0.16f;
                    var h = s * 0.28f;
                    var p = new PathF();
                    p.MoveTo(cx + w * 0.7f, cy - h);
                    p.LineTo(cx - w * 1.1f, cy);
                    p.LineTo(cx + w * 0.7f, cy + h);
                    canvas.DrawPath(p);
                    break;
                }

                case VectorIconKind.ChevronRight:
                {
                    var w = s * 0.16f;
                    var h = s * 0.28f;
                    var p = new PathF();
                    p.MoveTo(cx - w * 0.7f, cy - h);
                    p.LineTo(cx + w * 1.1f, cy);
                    p.LineTo(cx - w * 0.7f, cy + h);
                    canvas.DrawPath(p);
                    break;
                }

                case VectorIconKind.DoubleChevronLeft:
                {
                    var h = s * 0.25f;
                    var dx = s * 0.16f;
                    DrawChevron(canvas, cx - dx * 0.75f, cy, -s * 0.14f, h);
                    DrawChevron(canvas, cx + dx * 0.95f, cy, -s * 0.14f, h);
                    break;
                }

                case VectorIconKind.DoubleChevronRight:
                {
                    var h = s * 0.25f;
                    var dx = s * 0.16f;
                    DrawChevron(canvas, cx - dx * 0.95f, cy, s * 0.14f, h);
                    DrawChevron(canvas, cx + dx * 0.75f, cy, s * 0.14f, h);
                    break;
                }

                case VectorIconKind.Trash:
                {
                    var halfW = s * 0.26f;
                    var topY = cy - s * 0.24f;
                    var botY = cy + s * 0.34f;
                    // 顶盖横线 + 把手
                    canvas.DrawLine(cx - s * 0.34f, topY, cx + s * 0.34f, topY);
                    canvas.DrawLine(cx - s * 0.12f, topY - s * 0.11f, cx + s * 0.12f, topY - s * 0.11f);
                    canvas.DrawLine(cx - s * 0.12f, topY - s * 0.11f, cx - s * 0.12f, topY);
                    canvas.DrawLine(cx + s * 0.12f, topY - s * 0.11f, cx + s * 0.12f, topY);
                    // 桶身
                    var body = new PathF();
                    body.MoveTo(cx - halfW, topY);
                    body.LineTo(cx - halfW * 0.82f, botY);
                    body.LineTo(cx + halfW * 0.82f, botY);
                    body.LineTo(cx + halfW, topY);
                    canvas.DrawPath(body);
                    // 内部两条竖纹
                    canvas.DrawLine(cx - s * 0.09f, topY + s * 0.16f, cx - s * 0.07f, botY - s * 0.12f);
                    canvas.DrawLine(cx + s * 0.09f, topY + s * 0.16f, cx + s * 0.07f, botY - s * 0.12f);
                    break;
                }
            }

            canvas.RestoreState();
        }

        private static void DrawChevron(ICanvas canvas, float tipX, float cy, float dirW, float h)
        {
            var p = new PathF();
            p.MoveTo(tipX - dirW, cy - h);
            p.LineTo(tipX, cy);
            p.LineTo(tipX - dirW, cy + h);
            canvas.DrawPath(p);
        }
    }
}
