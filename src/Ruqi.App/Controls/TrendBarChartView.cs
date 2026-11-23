using Ruqi.Core.Models;
using Ruqi.Core.Services;

namespace Ruqi.App.Controls;

/// <summary>
/// 复刻 design_v2 统计页的双色圆角柱状图（当期浅靛蓝高亮 + 其余淡靛蓝，自带顶部金额提示与底部月份标签）。
/// </summary>
public sealed class TrendBarChartView : GraphicsView
{
    private IReadOnlyList<TrendBarPoint> _points = [];

    public TrendBarChartView()
    {
        HeightRequest = 186;
        Drawable = new ChartDrawable(this);
    }

    public void SetPoints(IReadOnlyList<TrendBarPoint> points)
    {
        _points = points ?? [];
        Invalidate();
    }

    private sealed class ChartDrawable(TrendBarChartView owner) : IDrawable
    {
        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            var pts = owner._points;
            if (pts.Count == 0 || dirtyRect.Width <= 20 || dirtyRect.Height <= 40)
            {
                return;
            }

            canvas.SaveState();

            var padLeft = 8f;
            var padRight = 8f;
            var padTop = 26f;
            var padBottom = 26f;

            var plotW = dirtyRect.Width - padLeft - padRight;
            var plotH = dirtyRect.Height - padTop - padBottom;
            var baseY = padTop + plotH;

            var maxCents = pts.Max(p => p.AmountCents);
            var safeMax = maxCents > 0 ? maxCents : 10000L;

            var count = pts.Count;
            var slotW = plotW / count;
            var barW = Math.Clamp(slotW * (count > 8 ? 0.56f : 0.48f), 10f, 32f);
            var corner = Math.Min(8f, barW / 2.2f);

            // 极淡底基准线
            canvas.StrokeColor = Color.FromArgb("#EDF0F7");
            canvas.StrokeSize = 1f;
            canvas.DrawLine(padLeft, baseY, padLeft + plotW, baseY);

            for (var i = 0; i < count; i++)
            {
                var pt = pts[i];
                var centerX = padLeft + slotW * (i + 0.5f);
                var ratio = pt.AmountCents > 0 ? Math.Clamp((float)pt.AmountCents / safeMax, 0.06f, 1f) : 0.03f;
                var barH = plotH * ratio;
                var barX = centerX - barW / 2f;
                var barY = baseY - barH;

                canvas.FillColor = pt.IsHighlighted
                    ? Color.FromArgb("#5B6CF9")
                    : pt.AmountCents > 0 ? Color.FromArgb("#D7DDFF") : Color.FromArgb("#EDF0F7");

                canvas.FillRoundedRectangle(barX, barY, barW, barH, corner);

                // 顶部数值标签（柱子较少时全显，12个月时仅显示高亮或非零中较大值防重叠）
                if (pt.AmountCents > 0 && (count <= 7 || pt.IsHighlighted))
                {
                    canvas.FontColor = pt.IsHighlighted ? Color.FromArgb("#5B6CF9") : Color.FromArgb("#68738A");
                    canvas.FontSize = count <= 7 ? 11f : 10f;
                    canvas.Font = pt.IsHighlighted ? Microsoft.Maui.Graphics.Font.DefaultBold : Microsoft.Maui.Graphics.Font.Default;
                    var text = FormatHelpers.FormatCny(pt.AmountCents, trimZeroDecimals: true);
                    canvas.DrawString(text, centerX - 32f, barY - 20f, 64f, 18f, HorizontalAlignment.Center, VerticalAlignment.Center);
                }

                // 底部月份标签
                canvas.FontColor = pt.IsHighlighted ? Color.FromArgb("#182033") : Color.FromArgb("#8A94A6");
                canvas.FontSize = count <= 7 ? 12f : 10f;
                canvas.Font = pt.IsHighlighted ? Microsoft.Maui.Graphics.Font.DefaultBold : Microsoft.Maui.Graphics.Font.Default;
                canvas.DrawString(pt.Label, centerX - slotW / 2f, baseY + 5f, slotW, 20f, HorizontalAlignment.Center, VerticalAlignment.Center);
            }

            canvas.RestoreState();
        }
    }
}
