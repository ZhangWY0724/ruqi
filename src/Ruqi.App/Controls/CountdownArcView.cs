namespace Ruqi.App.Controls;

/// <summary>
/// 首页弧形刻度倒计时表盘：统一以 30 天满刻度线性映射，还原 design_v2 弧形表盘质感。
/// </summary>
public sealed class CountdownArcView : GraphicsView
{
    public static readonly BindableProperty HasPlanProperty = BindableProperty.Create(
        nameof(HasPlan), typeof(bool), typeof(CountdownArcView), false,
        propertyChanged: (b, _, _) => ((CountdownArcView)b).Invalidate());

    public static readonly BindableProperty DaysRemainingProperty = BindableProperty.Create(
        nameof(DaysRemaining), typeof(int), typeof(CountdownArcView), 0,
        propertyChanged: (b, _, _) => ((CountdownArcView)b).Invalidate());

    public bool HasPlan
    {
        get => (bool)GetValue(HasPlanProperty);
        set => SetValue(HasPlanProperty, value);
    }

    public int DaysRemaining
    {
        get => (int)GetValue(DaysRemainingProperty);
        set => SetValue(DaysRemainingProperty, value);
    }

    public CountdownArcView()
    {
        HeightRequest = 176;
        WidthRequest = 240;
        HorizontalOptions = LayoutOptions.Center;
        Drawable = new ArcDrawable(this);
    }

    private sealed class ArcDrawable(CountdownArcView owner) : IDrawable
    {
        // 从左下 215° 顺时针扫过顶部 90° 到右下 -35°（总跨度 250°）
        private const float StartDeg = 215f;
        private const float SweepDeg = 250f;

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            var cx = dirtyRect.Center.X;
            var cy = dirtyRect.Y + dirtyRect.Height * 0.60f;
            var radius = Math.Min(dirtyRect.Width * 0.39f, dirtyRect.Height * 0.47f);
            if (radius <= 10) return;

            canvas.SaveState();

            // 1. 底层背景轨道
            canvas.StrokeColor = Color.FromArgb("#E6EAF5");
            canvas.StrokeSize = 10f;
            canvas.StrokeLineCap = LineCap.Round;
            canvas.DrawPath(BuildArcPath(cx, cy, radius, StartDeg, SweepDeg));

            // 2. 计算 30 天满刻度进度比例
            var progress = 0f;
            if (owner.HasPlan)
            {
                var clamped = Math.Clamp(owner.DaysRemaining, 0, 30);
                progress = clamped >= 30 ? 0.08f : Math.Clamp((30f - clamped) / 30f, 0.08f, 1f);
            }

            // 3. 外圈刻度短线（共 32 根）
            const int tickCount = 32;
            var tickInner = radius - 14f;
            var tickOuter = radius - 8f;
            for (var i = 0; i <= tickCount; i++)
            {
                var frac = i / (float)tickCount;
                var deg = StartDeg - frac * SweepDeg;
                var rad = deg * MathF.PI / 180f;
                var cos = MathF.Cos(rad);
                var sin = MathF.Sin(rad);

                var isLit = owner.HasPlan && frac <= progress + 0.01f;
                canvas.StrokeColor = isLit ? Color.FromArgb("#8B98FF") : Color.FromArgb("#D5DCF0");
                canvas.StrokeSize = i % 4 == 0 ? 2.1f : 1.3f;
                var inner = i % 4 == 0 ? tickInner - 3f : tickInner;
                canvas.DrawLine(cx + cos * inner, cy - sin * inner, cx + cos * tickOuter, cy - sin * tickOuter);
            }

            // 4. 前景浅靛蓝进度弧
            if (owner.HasPlan && progress > 0f)
            {
                canvas.StrokeColor = Color.FromArgb("#5B6CF9");
                canvas.StrokeSize = 10f;
                canvas.StrokeLineCap = LineCap.Round;
                canvas.DrawPath(BuildArcPath(cx, cy, radius, StartDeg, SweepDeg * progress));

                // 弧尾高亮圆点
                var endDeg = StartDeg - SweepDeg * progress;
                var endRad = endDeg * MathF.PI / 180f;
                var ex = cx + MathF.Cos(endRad) * radius;
                var ey = cy - MathF.Sin(endRad) * radius;
                canvas.FillColor = Colors.White;
                canvas.FillCircle(ex, ey, 3.2f);
            }

            // 5. 居中文字
            if (!owner.HasPlan)
            {
                canvas.FontColor = Color.FromArgb("#68738A");
                canvas.FontSize = 17f;
                canvas.Font = Microsoft.Maui.Graphics.Font.DefaultBold;
                canvas.DrawString("暂无计划续费", cx - 90, cy - 16, 180, 32, HorizontalAlignment.Center, VerticalAlignment.Center);
            }
            else
            {
                canvas.FontColor = Color.FromArgb("#68738A");
                canvas.FontSize = 13f;
                canvas.Font = Microsoft.Maui.Graphics.Font.Default;
                canvas.DrawString("距下次续费", cx - 70, cy - 40, 140, 22, HorizontalAlignment.Center, VerticalAlignment.Center);

                var mainText = owner.DaysRemaining <= 0 ? "今天" : $"{owner.DaysRemaining} 天";
                canvas.FontColor = owner.DaysRemaining <= 0 ? Color.FromArgb("#E89B2D") : Color.FromArgb("#182033");
                canvas.FontSize = 34f;
                canvas.Font = Microsoft.Maui.Graphics.Font.DefaultBold;
                canvas.DrawString(mainText, cx - 90, cy - 12, 180, 44, HorizontalAlignment.Center, VerticalAlignment.Center);
            }

            canvas.RestoreState();
        }

        private static PathF BuildArcPath(float cx, float cy, float r, float startDeg, float sweepClockwiseDeg)
        {
            const int segments = 72;
            var path = new PathF();
            for (var i = 0; i <= segments; i++)
            {
                var deg = startDeg - sweepClockwiseDeg * (i / (float)segments);
                var rad = deg * MathF.PI / 180f;
                var px = cx + MathF.Cos(rad) * r;
                var py = cy - MathF.Sin(rad) * r;
                if (i == 0)
                    path.MoveTo(px, py);
                else
                    path.LineTo(px, py);
            }

            return path;
        }
    }
}
