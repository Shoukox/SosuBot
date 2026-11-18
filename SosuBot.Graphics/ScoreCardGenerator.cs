using System.Globalization;
using System.Numerics;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SosuBot.Graphics.Models;
using static SosuBot.Graphics.CardDrawing;

namespace SosuBot.Graphics;

/// <summary>
/// Draws the /scorep picture: the beatmap's background at its own size with nothing but the score's statistics over
/// its lower part.
/// </summary>
public sealed class ScoreCardGenerator
{
    /// <summary>The size of the picture when the beatmap has no readable background.</summary>
    public const int FallbackWidth = 1920, FallbackHeight = 1080;

    /// <summary>Larger backgrounds are scaled down: Telegram shows photos at 1280 pixels at most anyway.</summary>
    public const int MaximumSide = 2560;

    // The block of statistics in the units of ScoreStatsPainter.
    private const float BlockHeight = 408;
    private const float GradeRowY = 40, ScoreY = 88, HitsRowY = 202, PpRowY = 294;

    private readonly FontFamily _extraBold = CardFonts.Shared.ExtraBold;

    /// <summary>The picture as JPEG: a photo at this size would be several times larger as PNG.</summary>
    public MemoryStream Generate(ScoreCardData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        using Image<Rgba32> card = LoadBackground(data.Background);
        float scale = Math.Min(card.Width / ScoreStatsPainter.Width, card.Height / BlockHeight);
        float blockHeight = BlockHeight * scale;
        // The block is centred horizontally and rests on the bottom edge.
        Matrix3x2 transform = Matrix3x2.CreateScale(scale) * Matrix3x2.CreateTranslation(
            (card.Width - ScoreStatsPainter.Width * scale) / 2, card.Height - blockHeight);

        card.Mutate(context =>
        {
            DrawShade(context, card.Width, card.Height, blockHeight);
            context.SetDrawingTransform(transform);
            DrawGradeRow(context, data, transform, scale);
            DrawStatistics(context, data, new ScoreStatsPainter(card.Width, card.Height, transform));
        });

        MemoryStream output = new();
        card.SaveAsJpeg(output, new JpegEncoder { Quality = 92 });
        output.Position = 0;
        return output;
    }

    private static Image<Rgba32> LoadBackground(byte[]? bytes)
    {
        Image<Rgba32>? background = TryLoad(bytes);
        if (background is null)
        {
            var gradient = new Image<Rgba32>(FallbackWidth, FallbackHeight);
            gradient.Mutate(context => context.Fill(new LinearGradientBrush(new PointF(0, 0),
                new PointF(FallbackWidth, FallbackHeight), GradientRepetitionMode.None,
                new ColorStop(0, Color.ParseHex("2B4552")), new ColorStop(1, Color.ParseHex("121C22")))));
            return gradient;
        }

        if (Math.Max(background.Width, background.Height) > MaximumSide)
        {
            background.Mutate(context => context.Resize(new ResizeOptions
            {
                Size = new Size(MaximumSide, MaximumSide),
                Mode = ResizeMode.Max
            }));
        }

        return background;
    }

    /// <summary>Darkens the picture a little, and its lower part enough for the statistics to read on any image.</summary>
    private static void DrawShade(IImageProcessingContext context, int width, int height, float blockHeight)
    {
        context.Fill(Color.Black.WithAlpha(0.22f));
        float top = Math.Max(0, height - blockHeight * 1.35f);
        context.Fill(new LinearGradientBrush(new PointF(0, top), new PointF(0, height - blockHeight * 0.55f),
                GradientRepetitionMode.None, new ColorStop(0, Color.Black.WithAlpha(0)),
                new ColorStop(1, Color.Black.WithAlpha(0.8f))),
            new RectangularPolygon(0, top, width, height - top));
    }

    private static void DrawStatistics(IImageProcessingContext context, ScoreCardData data, ScoreStatsPainter painter)
    {
        painter.DrawScore(context, data.Score, data.MaxScore, ScoreY);
        painter.DrawPillRow(context, HitsRowY,
        [
            ("Accuracy", $"{Number(data.AccuracyPercent, "0.00")}%"),
            ("Combo", $"{data.Combo}x/{data.MaxCombo}x"),
            .. data.Hits.Select(hit => (hit.Label, hit.Count.ToString(CultureInfo.InvariantCulture)))
        ]);
        painter.DrawPillRow(context, PpRowY,
        [
            ("PP", Pp(data.Pp)),
            ("FC", Pp(data.FullComboPp)),
            ("SS", Pp(data.PerfectPp))
        ]);
    }

    /// <summary>The icon of the game mode, the grade and the mods, centred above the score.</summary>
    private void DrawGradeRow(IImageProcessingContext context, ScoreCardData data, Matrix3x2 transform, float scale)
    {
        const float height = 52, gap = 10, padding = 26, iconSize = 58;
        Font gradeFont = _extraBold.CreateFont(38);
        Font modFont = _extraBold.CreateFont(26);
        (string Text, Color Color) grade = Grade(data.Rank);
        float gradeWidth = Measure(grade.Text, gradeFont).Width + 2 * padding;

        // As many mods as fit on the row.
        var mods = new List<(string Text, float Width)>();
        float total = iconSize + gap + gradeWidth;
        foreach (string mod in data.Mods)
        {
            float width = Measure(mod, modFont).Width + 28;
            if (total + gap + width > ScoreStatsPainter.Width - 80)
                break;
            mods.Add((mod, width));
            total += gap + width;
        }

        float x = (ScoreStatsPainter.Width - total) / 2;
        Vector2 iconCenter = Vector2.Transform(new Vector2(x + iconSize / 2, GradeRowY), transform);
        DrawModeIcon(context, data.Mode, new PointF(iconCenter.X, iconCenter.Y), iconSize * scale);
        x += iconSize + gap;

        context.Fill(grade.Color, RoundedRectangle(x, GradeRowY - height / 2, gradeWidth, height, 16));
        DrawText(context, grade.Text, gradeFont, Color.ParseHex("14202A"), x + padding, GradeRowY, VerticalAlignment.Center);
        x += gradeWidth + gap;
        foreach ((string text, float width) in mods)
        {
            context.Fill(Accent.WithAlpha(0.9f), RoundedRectangle(x, GradeRowY - height / 2 + 5, width, height - 10, 12));
            DrawText(context, text, modFont, AccentText, x + 14, GradeRowY, VerticalAlignment.Center);
            x += width + gap;
        }
    }

    private static (string Text, Color Color) Grade(string rank) => rank.ToUpperInvariant() switch
    {
        "XH" => ("SS", Color.ParseHex("E6EEF5")),
        "X" => ("SS", Color.ParseHex("FFD95E")),
        "SH" => ("S", Color.ParseHex("C9D6E0")),
        "S" => ("S", Color.ParseHex("FFC83D")),
        "A" => ("A", Color.ParseHex("6EDB7C")),
        "B" => ("B", Color.ParseHex("6FB8FF")),
        "C" => ("C", Color.ParseHex("C58BFF")),
        "D" => ("D", Color.ParseHex("FF6B6B")),
        _ => ("F", Color.ParseHex("9AA5AB"))
    };
}
