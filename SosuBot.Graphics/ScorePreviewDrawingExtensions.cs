using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace SosuBot.Graphics;

internal static class ScorePreviewDrawingExtensions
{
    public static void DrawGlowingText(
        this IImageProcessingContext context,
        string text,
        Font font,
        Color textColor,
        Color glowColor,
        float glowBlurRadius,
        Point drawPoint)
    {
        if (string.IsNullOrEmpty(text))
            return;

        RichTextOptions textOptions = CreateTextOptions(font);
        FontRectangle textSize = TextMeasurer.MeasureSize(text, textOptions);
        float padding = Math.Max(20, glowBlurRadius * 2);
        Point layerPoint = new(drawPoint.X - (int)padding, drawPoint.Y - (int)padding);
        int layerWidth = Math.Max(1, (int)Math.Ceiling(textSize.Width + padding * 4));
        int layerHeight = Math.Max(1, (int)Math.Ceiling(textSize.Height + padding * 4));

        using Image<Rgba32> glowLayer = new(layerWidth, layerHeight, Color.Transparent);
        using Image<Rgba32> finalLayer = new(layerWidth, layerHeight, Color.Transparent);
        RichTextOptions paddedOptions = new(textOptions)
        {
            Origin = new PointF(padding, padding)
        };

        if (glowBlurRadius > 0)
        {
            glowLayer.Mutate(glowContext =>
            {
                glowContext.DrawText(paddedOptions, text, glowColor);
                glowContext.GaussianBlur(glowBlurRadius);
            });
        }

        finalLayer.Mutate(finalContext =>
        {
            finalContext.DrawImage(glowLayer, 1);
            finalContext.DrawText(paddedOptions, text, textColor);
        });

        context.SetGraphicsOptions(new GraphicsOptions
        {
            Antialias = true,
            AlphaCompositionMode = PixelAlphaCompositionMode.SrcOver
        });
        context.DrawImage(finalLayer, layerPoint, 1);
    }

    public static void DrawGradientGlowingText(
        this IImageProcessingContext context,
        string text,
        Font font,
        ColorStop[] textColors,
        ColorStop[] glowColors,
        float glowBlurRadius,
        Point drawPoint,
        Point? gradientOffset = null,
        TextAlignment textAlignment = TextAlignment.Start,
        bool verticalGradient = true,
        bool softenGlow = false)
    {
        if (string.IsNullOrEmpty(text))
            return;

        RichTextOptions textOptions = CreateTextOptions(font, textAlignment);
        FontRectangle textSize = TextMeasurer.MeasureSize(text, textOptions);
        float padding = Math.Max(20, glowBlurRadius * 2);
        Point layerPoint = new(drawPoint.X - (int)padding, drawPoint.Y - (int)padding);
        int layerWidth = Math.Max(1, (int)Math.Ceiling(textSize.Width + padding * 4));
        int layerHeight = Math.Max(1, (int)Math.Ceiling(textSize.Height * 1.5 + padding * 4));

        using Image<Rgba32> glowLayer = new(layerWidth, layerHeight, Color.Transparent);
        using Image<Rgba32> finalLayer = new(layerWidth, layerHeight, Color.Transparent);
        RichTextOptions paddedOptions = new(textOptions)
        {
            Origin = new PointF(padding, padding)
        };

        Point gradientStart;
        Point gradientEnd;
        if (verticalGradient)
        {
            gradientStart = new Point(0,
                (int)paddedOptions.Origin.Y + (int)font.Size / 10 + (gradientOffset?.X ?? 0));
            gradientEnd = new Point(0,
                (int)paddedOptions.Origin.Y + (int)textSize.Height + (gradientOffset?.Y ?? 0));
        }
        else
        {
            gradientStart = new Point(
                (int)paddedOptions.Origin.X + (int)font.Size / 10 + (gradientOffset?.X ?? 0), 0);
            gradientEnd = new Point(
                Math.Min(ScorePreviewGenerator.PreviewWidth,
                    (int)paddedOptions.Origin.X + (int)textSize.Width + (gradientOffset?.Y ?? 0)), 0);
        }

        ColorStop[] effectiveGlowColors = softenGlow
            ? glowColors.Select(stop => new ColorStop(stop.Ratio, stop.Color.WithAlpha(0.5f))).ToArray()
            : glowColors;

        if (glowBlurRadius > 0)
        {
            glowLayer.Mutate(glowContext =>
            {
                glowContext.DrawText(paddedOptions, text,
                    new LinearGradientBrush(gradientStart, gradientEnd, GradientRepetitionMode.None,
                        effectiveGlowColors));
                glowContext.GaussianBlur(glowBlurRadius * (softenGlow ? 1 : 0.75f));
            });
        }

        finalLayer.Mutate(finalContext =>
        {
            finalContext.DrawImage(glowLayer, 1);
            finalContext.DrawText(paddedOptions, text,
                new LinearGradientBrush(gradientStart, gradientEnd, GradientRepetitionMode.None, textColors));
        });

        context.SetGraphicsOptions(new GraphicsOptions
        {
            Antialias = true,
            AlphaCompositionMode = PixelAlphaCompositionMode.SrcOver
        });
        context.DrawImage(finalLayer, layerPoint, 1);
    }

    private static RichTextOptions CreateTextOptions(Font font,
        TextAlignment textAlignment = TextAlignment.Start) => new(font)
    {
        Origin = PointF.Empty,
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top,
        KerningMode = KerningMode.Standard,
        TextAlignment = textAlignment
    };
}
