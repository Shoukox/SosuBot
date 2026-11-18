using System.Globalization;
using System.Numerics;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using static SosuBot.Graphics.CardDrawing;

namespace SosuBot.Graphics;

/// <summary>
/// The score with its progress bar and the rows of statistics, laid out on a block <see cref="Width" /> units wide.
/// <paramref name="transform" /> places that block on the canvas, so the same layout fits a background of any size.
/// </summary>
internal sealed class ScoreStatsPainter(int canvasWidth, int canvasHeight, Matrix3x2 transform)
{
    public const float Width = 1080;
    private const float Margin = 40;

    // Taller than any block: the clipping shapes only have to cover the bar vertically.
    private const float ClipHeight = 10_000;

    private readonly FontFamily _extraBold = CardFonts.Shared.ExtraBold;

    /// <summary>
    /// The score with a progress bar from 0 on the left to the highest standardised score on the right. Where the
    /// bar reaches the digits it wraps around them in a rounded frame and goes on behind them; the progress fills
    /// the frame too. A score above the maximum is a classic one: the bar is full and its ends are not labelled.
    /// </summary>
    public void DrawScore(IImageProcessingContext context, long scoreValue, long? maxScore, float originY)
    {
        Font scoreFont = _extraBold.CreateFont(80);
        string score = scoreValue.ToString("N0", CultureInfo.InvariantCulture);
        var scoreOptions = new RichTextOptions(scoreFont)
        {
            Origin = new PointF((Width - Measure(score, scoreFont).Width) / 2, originY),
            KerningMode = KerningMode.Standard
        };
        FontRectangle digits = TextMeasurer.MeasureBounds(score, scoreOptions);
        float barY = (digits.Top + digits.Bottom) / 2;

        const float thickness = 10;
        bool withinRange = maxScore is > 0 && scoreValue <= maxScore;
        float barStart = Margin, barEnd = Width - Margin;
        if (withinRange)
        {
            Font edgeFont = _extraBold.CreateFont(22);
            string max = maxScore!.Value.ToString("N0", CultureInfo.InvariantCulture);
            barStart += DrawText(context, "0", edgeFont, SoftText, Margin, barY, VerticalAlignment.Center) + 14;
            float maxWidth = Measure(max, edgeFont).Width;
            DrawText(context, max, edgeFont, SoftText, Width - Margin - maxWidth, barY, VerticalAlignment.Center);
            barEnd -= maxWidth + 14;
        }

        // The frame around the digits, with the bar running into its sides.
        float frameHeight = digits.Height + 34;
        float frameLeft = digits.Left - 30, frameRight = digits.Right + 30;
        IPath frame = RoundedRectangle(frameLeft, barY - frameHeight / 2, frameRight - frameLeft, frameHeight,
            frameHeight / 2);
        // The bar's outer ends are round; its inner ends stop square on the middle of the frame's line, so nothing
        // pokes into the frame.
        IPath[] track =
        [
            RoundedRectangle(barStart, barY - thickness / 2, frameLeft - barStart + thickness, thickness, thickness / 2)
                .Clip(new RectangularPolygon(frameLeft, 0, thickness * 2, ClipHeight)),
            frame.GenerateOutline(thickness),
            RoundedRectangle(frameRight - thickness, barY - thickness / 2, barEnd - frameRight + thickness, thickness,
                    thickness / 2)
                .Clip(new RectangularPolygon(frameRight - thickness * 2, 0, thickness * 2, ClipHeight))
        ];

        // Each colour is drawn opaque on its own layer first: the parts merge into one line, and the translucent
        // track does not get brighter where they overlap.
        using Image<Rgba32> trackLayer = new(canvasWidth, canvasHeight);
        trackLayer.Mutate(layer =>
        {
            layer.SetDrawingTransform(transform);
            foreach (IPath part in track)
                layer.Fill(Color.White, part);
        });
        context.DrawImage(trackLayer, new Point(0, 0), 0.16f);

        // The filled part is the track with everything right of the progress cut away.
        float progress = withinRange ? (float)Math.Clamp((double)scoreValue / maxScore!.Value, 0, 1) : 1;
        if (progress > 0)
        {
            float progressX = barStart + (barEnd - barStart) * progress;
            var rest = new RectangularPolygon(progressX, 0, Width - progressX + 1, ClipHeight);
            using Image<Rgba32> filledLayer = new(canvasWidth, canvasHeight);
            filledLayer.Mutate(layer =>
            {
                layer.SetDrawingTransform(transform);
                foreach (IPath part in track)
                    layer.Fill(Accent, progress >= 1 ? part : part.Clip(rest));
            });
            context.DrawImage(filledLayer, new Point(0, 0), 1);
        }

        context.DrawText(scoreOptions, score, Accent);
    }

    /// <summary>Label pills with the value under each, centred as a row.</summary>
    public void DrawPillRow(IImageProcessingContext context, float y, (string Label, string Value)[] items)
    {
        if (items.Length == 0)
            return;

        const float gap = 14, minimumLabelSize = 15;
        float labelSize = 23, padding = 36;
        Font labelFont, valueFont;
        float[] widths;
        // Mania has the most hit results: shrink the row until it fits the card.
        while (true)
        {
            labelFont = _extraBold.CreateFont(labelSize);
            valueFont = _extraBold.CreateFont(labelSize * 29 / 23);
            Font currentLabelFont = labelFont, currentValueFont = valueFont;
            float currentPadding = padding;
            widths = items
                .Select(item => Math.Max(Measure(item.Label, currentLabelFont).Width,
                    Measure(item.Value, currentValueFont).Width) + currentPadding)
                .ToArray();
            if (widths.Sum() + gap * (items.Length - 1) <= Width - 48 || labelSize <= minimumLabelSize)
                break;

            labelSize -= 1;
            padding = Math.Max(18, padding - 2);
        }

        float x = (Width - widths.Sum() - gap * (items.Length - 1)) / 2;
        for (var i = 0; i < items.Length; i++)
        {
            float center = x + widths[i] / 2;
            context.Fill(PillFill, RoundedRectangle(x, y, widths[i], 38, 19));
            DrawText(context, items[i].Label, labelFont, SoftText, center - Measure(items[i].Label, labelFont).Width / 2,
                y + 19, VerticalAlignment.Center);
            DrawText(context, items[i].Value, valueFont, Color.White,
                center - Measure(items[i].Value, valueFont).Width / 2, y + 60, VerticalAlignment.Center);
            x += widths[i] + gap;
        }
    }
}
