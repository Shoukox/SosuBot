using System.Globalization;
using System.Numerics;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SosuBot.Graphics.Models;
using static SosuBot.Graphics.CardDrawing;

namespace SosuBot.Graphics;

/// <summary>
/// Draws the card the bot answers a replay file with: the beatmap over its blurred cover on top (status, length,
/// max combo, bpm, title, CS/AR/HP/OD, difficulty and stars), the play on a panel tinted by the cover below
/// (score, accuracy, combo, pp with its FC and SS values, hit counts).
/// </summary>
public sealed class ReplayCardGenerator
{
    public const int Width = 1080;
    public const int Height = 620;
    private const int HeaderHeight = 300;
    private const int Margin = 40;

    private static readonly Color FallbackTint = Color.ParseHex("1B2D36");

    private readonly FontFamily _extraBold = CardFonts.Shared.ExtraBold;

    /// <summary>The card as PNG.</summary>
    public MemoryStream Generate(ReplayCardData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        using Image<Rgba32> card = new(Width, Height);
        using Image<Rgba32>? cover = TryLoad(data.Background);
        Color tint = cover is null ? FallbackTint : PanelTint(cover);

        card.Mutate(context =>
        {
            DrawBackground(context, cover, tint);
            DrawHeader(context, data);
            DrawPlay(context, data);
        });

        MemoryStream output = new();
        card.Save(output, new PngEncoder());
        output.Position = 0;
        return output;
    }

    /// <summary>The blurred cover behind the beatmap part, a solid panel in the cover's colour under the play.</summary>
    private static void DrawBackground(IImageProcessingContext context, Image<Rgba32>? cover, Color tint)
    {
        context.Fill(tint);
        if (cover is not null)
        {
            cover.Mutate(image => image
                .Resize(new ResizeOptions
                {
                    Size = new Size(Width, HeaderHeight),
                    Mode = ResizeMode.Crop,
                    Position = AnchorPositionMode.Center
                })
                .GaussianBlur(3));
            context.DrawImage(cover, new Point(0, 0), 1);
        }
        else
        {
            context.Fill(new LinearGradientBrush(new PointF(0, 0), new PointF(Width, HeaderHeight),
                GradientRepetitionMode.None, new ColorStop(0, Lighten(tint, 0.25f)), new ColorStop(1, tint)),
                new RectangularPolygon(0, 0, Width, HeaderHeight));
        }

        // Darker on the left where the text is, so it reads on any cover.
        context.Fill(new LinearGradientBrush(new PointF(0, 0), new PointF(Width, 0), GradientRepetitionMode.None,
                new ColorStop(0, Color.Black.WithAlpha(0.62f)), new ColorStop(0.55f, Color.Black.WithAlpha(0.4f)),
                new ColorStop(1, Color.Black.WithAlpha(0.2f))),
            new RectangularPolygon(0, 0, Width, HeaderHeight));
    }

    private void DrawHeader(IImageProcessingContext context, ReplayCardData data)
    {
        // Title and artist.
        Font titleFont = Fit(data.Title, _extraBold, 46, 30, Width - 2 * Margin);
        DrawText(context, Ellipsize(data.Title, titleFont, Width - 2 * Margin), titleFont, Color.White, Margin, 26);
        // The artist, with the status pill right after it.
        const float artistY = 98;
        Font statusFont = _extraBold.CreateFont(21);
        string status = StatusName(data.Status);
        float statusPillWidth = 40 + Measure(status, statusFont).Width + 16;
        Font artistFont = _extraBold.CreateFont(26);
        float statusX = Margin + 16 + DrawText(context,
            Ellipsize(data.Artist, artistFont, Width - 2 * Margin - statusPillWidth - 16), artistFont, SoftText,
            Margin, artistY, VerticalAlignment.Center);
        context.Fill(Color.Black.WithAlpha(0.35f), RoundedRectangle(statusX, artistY - 18, statusPillWidth, 36, 18));
        context.Fill(StatusColor(data.Status), new EllipsePolygon(statusX + 18, artistY, 13));
        DrawChevrons(context, new PointF(statusX + 18, artistY), Color.ParseHex("12323A"), 0.8f);
        DrawText(context, status, statusFont, Color.White, statusX + 38, artistY, VerticalAlignment.Center);

        // CS, AR, HP and OD bars on the left.
        Font barFont = _extraBold.CreateFont(22);
        (string Name, double Value)[] bars = data.Mode == OsuGameMode.Mania
            ? [("Keys", data.CircleSize), ("HP", data.Drain), ("OD", data.OverallDifficulty)]
            : data.Mode == OsuGameMode.Taiko
                ? [("HP", data.Drain), ("OD", data.OverallDifficulty)]
                : [("CS", data.CircleSize), ("AR", data.ApproachRate), ("HP", data.Drain), ("OD", data.OverallDifficulty)];
        float barY = 156;
        // The bars start after the widest label ("Keys" in mania is wider than "CS").
        float barX = Margin + Math.Max(46, bars.Max(bar => Measure(bar.Name, barFont).Width) + 12);
        float barWidth = 368 - barX;
        foreach ((string name, double value) in bars)
        {
            DrawText(context, name, barFont, Color.White, Margin, barY, VerticalAlignment.Center);
            context.Fill(Color.White.WithAlpha(0.25f), RoundedRectangle(barX, barY - 6, barWidth, 12, 6));
            float filled = (float)Math.Clamp(value / (name == "Keys" ? 18 : 10), 0, 1) * barWidth;
            if (filled > 0)
                context.Fill(Color.White, RoundedRectangle(barX, barY - 6, Math.Max(filled, 12), 12, 6));
            string valueText = name == "Keys" ? Number(value, "0") : Number(value, "0.0");
            DrawText(context, valueText, barFont, Color.White, 420 - Measure(valueText, barFont).Width, barY,
                VerticalAlignment.Center);
            barY += 36;
        }

        // On the right: difficulty and mapper; length, max combo and bpm; stars and mods.
        const float column = 470;
        const float difficultyY = 156, infoY = 200, starsY = 244;

        DrawModeIcon(context, data.Mode, new PointF(column + 14, difficultyY), 32);
        Font difficultyFont = Fit(data.DifficultyName, _extraBold, 27, 20, 360);
        string difficulty = Ellipsize(data.DifficultyName, difficultyFont, 360);
        float x = column + 38;
        x += DrawText(context, difficulty, difficultyFont, Color.White, x, difficultyY, VerticalAlignment.Center) + 12;
        Font mapperFont = _extraBold.CreateFont(21);
        DrawText(context, Ellipsize($"by {data.Mapper}", mapperFont, Width - Margin - x), mapperFont, SoftText,
            x, difficultyY + 2, VerticalAlignment.Center);

        Font infoFont = _extraBold.CreateFont(24);
        x = column + 2;
        DrawClock(context, new PointF(x + 12, infoY));
        x += 30;
        x += DrawText(context, Duration(data.LengthSeconds), infoFont, Color.White, x, infoY, VerticalAlignment.Center) + 22;
        DrawLink(context, new PointF(x + 12, infoY));
        x += 30;
        x += DrawText(context, $"{data.MaxCombo}x", infoFont, Color.White, x, infoY, VerticalAlignment.Center) + 22;
        DrawMetronome(context, new PointF(x + 11, infoY));
        x += 28;
        DrawText(context, $"{Number(data.Bpm, "0.##")} bpm", infoFont, Color.White, x, infoY, VerticalAlignment.Center);

        float starX = column + 2;
        for (var i = 0; i < 5; i++)
        {
            double part = Math.Clamp(data.StarRating - i, 0, 1);
            float size = part >= 1 ? 30 : 14 + 16 * (float)part;
            Color color = part > 0 ? Color.White : Color.White.WithAlpha(0.3f);
            context.Fill(color, Star(new PointF(starX + 15, starsY), size / 2));
            starX += 34;
        }

        Font starFont = _extraBold.CreateFont(24);
        starX += 8 + DrawText(context, Number(data.StarRating, "0.00"), starFont, Color.White, starX + 4, starsY,
            VerticalAlignment.Center) + 20;

        Font modFont = _extraBold.CreateFont(20);
        foreach (string mod in data.Mods)
        {
            float modWidth = Measure(mod, modFont).Width + 22;
            if (starX + modWidth > Width - Margin)
                break;
            context.Fill(Accent.WithAlpha(0.85f), RoundedRectangle(starX, starsY - 15, modWidth, 30, 10));
            DrawText(context, mod, modFont, AccentText, starX + 11, starsY, VerticalAlignment.Center);
            starX += modWidth + 8;
        }
    }

    private static void DrawPlay(IImageProcessingContext context, ReplayCardData data)
    {
        var painter = new ScoreStatsPainter(Width, Height, Matrix3x2.Identity);
        painter.DrawScore(context, data.Score, data.MaxScore, 330);

        painter.DrawPillRow(context, 444,
        [
            ("Accuracy", $"{Number(data.AccuracyPercent, "0.00")}%"),
            ("Combo", $"{data.Combo}x/{data.MaxCombo}x"),
            .. data.Hits.Select(hit => (hit.Label, hit.Count.ToString(CultureInfo.InvariantCulture)))
        ]);
        painter.DrawPillRow(context, 536,
        [
            ("PP", Pp(data.Pp)),
            ("FC", Pp(data.FullComboPp)),
            ("SS", Pp(data.PerfectPp))
        ]);
    }

    private static void DrawChevrons(IImageProcessingContext context, PointF center, Color color, float scale = 1)
    {
        foreach (float offset in new[] { -4f * scale, 4f * scale })
        {
            context.DrawLine(color, 3.2f * scale,
                new PointF(center.X - 7 * scale, center.Y + offset + 3.5f * scale),
                new PointF(center.X, center.Y + offset - 3.5f * scale),
                new PointF(center.X + 7 * scale, center.Y + offset + 3.5f * scale));
        }
    }

    private static void DrawClock(IImageProcessingContext context, PointF center)
    {
        context.Draw(Color.White, 3.5f, new EllipsePolygon(center, 11.5f));
        context.DrawLine(Color.White, 3, new PointF(center.X, center.Y - 6.5f), center,
            new PointF(center.X + 5, center.Y + 3));
    }

    private static void DrawLink(IImageProcessingContext context, PointF center)
    {
        // Two chain links at 45 degrees.
        IPath link = RoundedRectangle(-11, -5, 16, 10, 5);
        context.Draw(Color.White, 3, link.Rotate(-MathF.PI / 4).Translate(center.X - 3, center.Y + 3));
        context.Draw(Color.White, 3, link.Rotate(-MathF.PI / 4).Translate(center.X + 4, center.Y - 4));
    }

    private static void DrawMetronome(IImageProcessingContext context, PointF center)
    {
        context.Fill(Color.White, new Polygon(new LinearLineSegment(
            new PointF(center.X - 11, center.Y + 11), new PointF(center.X - 4, center.Y - 11),
            new PointF(center.X + 4, center.Y - 11), new PointF(center.X + 11, center.Y + 11))));
        context.DrawLine(Color.ParseHex("12323A"), 2.5f, new PointF(center.X - 1, center.Y + 7),
            new PointF(center.X + 9, center.Y - 9));
    }

    private static IPath Star(PointF center, float radius)
    {
        var points = new PointF[10];
        for (var i = 0; i < 10; i++)
        {
            double angle = -Math.PI / 2 + i * Math.PI / 5;
            float r = i % 2 == 0 ? radius : radius * 0.48f;
            points[i] = new PointF(center.X + r * (float)Math.Cos(angle), center.Y + r * (float)Math.Sin(angle));
        }

        return new Polygon(new LinearLineSegment(points));
    }

    /// <summary>A dark tint of the cover's average colour for the lower panel.</summary>
    private static Color PanelTint(Image<Rgba32> cover)
    {
        using Image<Rgba32> pixel = cover.Clone(image => image.Resize(1, 1));
        Rgba32 average = pixel[0, 0];
        // Keep the hue, but dark enough for white text.
        return Color.FromRgb((byte)(average.R * 0.3f + 8), (byte)(average.G * 0.3f + 12), (byte)(average.B * 0.3f + 14));
    }

    private static string StatusName(string status) => status.ToLowerInvariant() switch
    {
        "ranked" => "Ranked",
        "approved" => "Approved",
        "qualified" => "Qualified",
        "loved" => "Loved",
        "pending" => "Pending",
        "wip" => "WIP",
        "graveyard" => "Graveyard",
        _ => "Unknown"
    };

    private static Color StatusColor(string status) => Color.ParseHex(status.ToLowerInvariant() switch
    {
        "ranked" or "approved" => "5FD9E8",
        "qualified" => "6FC3FF",
        "loved" => "FF66AA",
        "pending" or "wip" => "FFD966",
        _ => "9AA5AB"
    });

    private static string Duration(int seconds) =>
        $"{Math.Max(seconds, 0) / 60:00}:{Math.Max(seconds, 0) % 60:00}";
}
