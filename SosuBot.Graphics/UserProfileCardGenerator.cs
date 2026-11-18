using System.Globalization;
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
/// Draws the /userp picture, the /userw widget as an image: the blurred banner behind the avatar and name, the mode
/// tabs, pp and rank, the level bar, the main statistics and the grade counts.
/// </summary>
public sealed class UserProfileCardGenerator
{
    public const int Width = 1080;
    public const int Height = 990;
    private const int Margin = 56;
    private const int ContentWidth = Width - 2 * Margin;

    private static readonly Color Muted = Color.White.WithAlpha(0.72f);
    private static readonly Color Chip = Color.White.WithAlpha(0.14f);
    private static readonly Color Difference = Color.ParseHex("9FE0FF");
    private static readonly Color Silver = Color.ParseHex("B8C4CF");
    private static readonly Color Gold = Color.ParseHex("F0B90B");
    private static readonly Color Green = Color.ParseHex("5FD068");

    private readonly FontFamily _bold = CardFonts.Shared.Bold;
    private readonly FontFamily _extraBold = CardFonts.Shared.ExtraBold;

    /// <summary>The card as PNG.</summary>
    public MemoryStream Generate(UserProfileCardData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        using Image<Rgba32> card = new(Width, Height);
        using Image<Rgba32>? banner = TryLoad(data.Banner);
        using Image<Rgba32>? avatar = TryLoad(data.Avatar);

        card.Mutate(context =>
        {
            DrawBackground(context, banner, data.Avatar);
            DrawHeader(context, data, avatar);
            DrawTabs(context, data.Mode);
            DrawRanking(context, data);
            DrawStatistics(context, data);
            DrawGrades(context, data);
            DrawFooter(context, data);
        });

        MemoryStream output = new();
        card.Save(output, new PngEncoder());
        output.Position = 0;
        return output;
    }

    private static void DrawBackground(IImageProcessingContext context, Image<Rgba32>? banner, byte[]? avatar)
    {
        if (banner is not null)
        {
            banner.Mutate(image => image
                .Resize(new ResizeOptions { Size = new Size(Width, Height), Mode = ResizeMode.Crop })
                .GaussianBlur(12)
                .Brightness(0.4f));
            context.DrawImage(banner, new Point(0, 0), 1);
            return;
        }

        (string Accent, string Shade) colors = WidgetArtwork.GetAccentColors(avatar) ?? ("#2B4552", "#121C22");
        context.Fill(new LinearGradientBrush(new PointF(0, 0), new PointF(Width, Height), GradientRepetitionMode.None,
            new ColorStop(0, Color.ParseHex(colors.Accent)), new ColorStop(1, Color.ParseHex(colors.Shade))));
    }

    private void DrawHeader(IImageProcessingContext context, UserProfileCardData data, Image<Rgba32>? avatar)
    {
        const int size = 148, top = 44;
        var center = new PointF(Margin + size / 2f, top + size / 2f);
        if (avatar is not null)
        {
            avatar.Mutate(image => image.Resize(new ResizeOptions { Size = new Size(size, size), Mode = ResizeMode.Crop }));
            CutToCircle(avatar);
            context.DrawImage(avatar, new Point(Margin, top), 1);
        }
        else
        {
            context.Fill(Color.White, new EllipsePolygon(center, size / 2f));
            Font letterFont = _extraBold.CreateFont(72);
            string letter = data.Username.Length > 0 ? data.Username[..1].ToUpperInvariant() : "?";
            DrawText(context, letter, letterFont, Color.ParseHex("111111"),
                center.X - Measure(letter, letterFont).Width / 2, center.Y, VerticalAlignment.Center);
        }

        context.Draw(Color.White.WithAlpha(0.85f), 5, new EllipsePolygon(center, size / 2f));

        const float textX = Margin + size + 32;
        float textWidth = Width - Margin - textX;
        Font nameFont = Fit(data.Username, _extraBold, 60, 36, textWidth);
        DrawText(context, Ellipsize(data.Username, nameFont, textWidth), nameFont, Color.White, textX, center.Y - 28,
            VerticalAlignment.Center);

        string country = data.CountryCode is { Length: > 0 } code ? $" · {code.ToUpperInvariant()} {Rank(data.CountryRank)}" : "";
        DrawText(context, Rank(data.GlobalRank) + country, _bold.CreateFont(34), Muted, textX, center.Y + 36,
            VerticalAlignment.Center);
    }

    private static void CutToCircle(Image<Rgba32> image)
    {
        IPath corners = new RectangularPolygon(0, 0, image.Width, image.Height)
            .Clip(new EllipsePolygon(image.Width / 2f, image.Height / 2f, image.Width / 2f));
        image.Mutate(context => context
            .SetGraphicsOptions(new GraphicsOptions { AlphaCompositionMode = PixelAlphaCompositionMode.DestOut })
            .Fill(Color.Black, corners));
    }

    private void DrawTabs(IImageProcessingContext context, OsuGameMode mode)
    {
        const float top = 222, height = 64, gap = 12, iconSize = 42, iconGap = 10;
        string[] names = ["osu!", "taiko", "catch", "mania"];
        float width = (ContentWidth - gap * (names.Length - 1)) / names.Length;
        Font font = _extraBold.CreateFont(30);
        for (var i = 0; i < names.Length; i++)
        {
            float x = Margin + i * (width + gap);
            bool active = i == (int)mode;
            context.Fill(active ? Color.White : Chip, RoundedRectangle(x, top, width, height, 18));

            // The icon and the name are centred together.
            float nameWidth = Measure(names[i], font).Width;
            float iconX = x + (width - iconSize - iconGap - nameWidth) / 2;
            var iconCenter = new PointF(iconX + iconSize / 2, top + height / 2);
            // The symbols inside the icons are white: on the white active tab they need a dark backing.
            if (active)
                context.Fill(Color.ParseHex("2A2A33"), new EllipsePolygon(iconCenter, iconSize * 0.42f));
            DrawModeIcon(context, (OsuGameMode)i, iconCenter, iconSize);
            DrawText(context, names[i], font, active ? Color.ParseHex("111111") : Color.White,
                iconX + iconSize + iconGap, top + height / 2, VerticalAlignment.Center);
        }
    }

    private void DrawRanking(IImageProcessingContext context, UserProfileCardData data)
    {
        const float labelY = 330, valueY = 396;
        Font labelFont = _bold.CreateFont(30);
        DrawText(context, "pp", labelFont, Muted, Margin, labelY, VerticalAlignment.Center);
        Font ppFont = _extraBold.CreateFont(84);
        float ppWidth = DrawText(context, Whole(data.Pp), ppFont, Color.White, Margin, valueY, VerticalAlignment.Center);
        DrawText(context, data.PpDifference, _bold.CreateFont(32), Difference, Margin + ppWidth + 16, valueY + 14,
            VerticalAlignment.Center);

        const string rankLabel = "Global rank";
        DrawText(context, rankLabel, labelFont, Muted, Width - Margin - Measure(rankLabel, labelFont).Width, labelY,
            VerticalAlignment.Center);
        Font rankFont = _extraBold.CreateFont(62);
        string rank = Rank(data.GlobalRank);
        DrawText(context, rank, rankFont, Color.White, Width - Margin - Measure(rank, rankFont).Width, valueY,
            VerticalAlignment.Center);

        // The level bar.
        const float barY = 466;
        float progress = Math.Clamp(data.LevelProgress ?? 0, 0, 100) / 100f;
        context.Fill(Chip, RoundedRectangle(Margin, barY, ContentWidth, 12, 6));
        if (progress > 0)
            context.Fill(Color.White, RoundedRectangle(Margin, barY, Math.Max(ContentWidth * progress, 12), 12, 6));
        DrawText(context, $"lvl {Count(data.Level)} · {data.LevelProgress ?? 0}%", _bold.CreateFont(28), Muted, Margin,
            barY + 38, VerticalAlignment.Center);
    }

    private void DrawStatistics(IImageProcessingContext context, UserProfileCardData data)
    {
        const float firstRow = 552, rowHeight = 108;
        string accuracy = data.AccuracyPercent is { } percent ? $"{Number(percent, "0.00")}%" : "—";
        string playTime = data.PlayTimeHours is { } hours ? $"{Count(hours)} h" : "—";
        string combo = data.MaximumCombo is { } maximum ? $"{Count(maximum)}x" : "—";

        DrawStatistic(context, Margin, firstRow, "Accuracy", accuracy, data.AccuracyDifference);
        DrawRightStatistic(context, firstRow, "Play count", Count(data.PlayCount));
        DrawStatistic(context, Margin, firstRow + rowHeight, "Play time", playTime, data.PlayTimeDifference);
        DrawRightStatistic(context, firstRow + rowHeight, "Max combo", combo);
    }

    /// <summary>A statistic ending at the right edge of the content, under the global rank.</summary>
    private void DrawRightStatistic(IImageProcessingContext context, float y, string label, string value)
    {
        Font labelFont = _bold.CreateFont(28);
        Font valueFont = _extraBold.CreateFont(44);
        DrawText(context, label, labelFont, Muted, Width - Margin - Measure(label, labelFont).Width, y,
            VerticalAlignment.Center);
        DrawText(context, value, valueFont, Color.White, Width - Margin - Measure(value, valueFont).Width, y + 46,
            VerticalAlignment.Center);
    }

    private void DrawStatistic(IImageProcessingContext context, float x, float y, string label, string value,
        string difference)
    {
        DrawText(context, label, _bold.CreateFont(28), Muted, x, y, VerticalAlignment.Center);
        float width = DrawText(context, value, _extraBold.CreateFont(44), Color.White, x, y + 46, VerticalAlignment.Center);
        DrawText(context, difference, _bold.CreateFont(26), Difference, x + width + 12, y + 52, VerticalAlignment.Center);
    }

    private void DrawGrades(IImageProcessingContext context, UserProfileCardData data)
    {
        const float top = 768, height = 124, gap = 14;
        (string Label, Color Color, int? Count)[] grades =
        [
            ("SS", Silver, data.SilverSs),
            ("SS", Gold, data.Ss),
            ("S", Silver, data.SilverS),
            ("S", Gold, data.S),
            ("A", Green, data.A)
        ];
        float width = (ContentWidth - gap * (grades.Length - 1)) / grades.Length;
        Font labelFont = _extraBold.CreateFont(36);
        Font countFont = _bold.CreateFont(32);
        for (var i = 0; i < grades.Length; i++)
        {
            float x = Margin + i * (width + gap);
            context.Fill(Chip, RoundedRectangle(x, top, width, height, 18));
            DrawText(context, grades[i].Label, labelFont, grades[i].Color,
                x + (width - Measure(grades[i].Label, labelFont).Width) / 2, top + 40, VerticalAlignment.Center);
            string count = Count(grades[i].Count);
            DrawText(context, count, countFont, Color.White, x + (width - Measure(count, countFont).Width) / 2, top + 88,
                VerticalAlignment.Center);
        }
    }

    private void DrawFooter(IImageProcessingContext context, UserProfileCardData data)
    {
        Font font = _bold.CreateFont(28);
        string text = $"Registered: {data.Registered ?? "—"} · Achievements: {data.Achievements ?? "—"}";
        DrawText(context, Ellipsize(text, font, ContentWidth), font, Muted, Margin, 938, VerticalAlignment.Center);
    }

    private static string Rank(int? rank) => rank is { } value ? $"#{Count(value)}" : "#—";

    private static string Whole(double? value) =>
        value is { } number && double.IsFinite(number) ? number.ToString("N0", CultureInfo.InvariantCulture) : "—";

    private static string Count(long? value) => value?.ToString("N0", CultureInfo.InvariantCulture) ?? "—";
}
