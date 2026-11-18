using System.Globalization;
using System.Reflection;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SosuBot.Graphics.Models;

namespace SosuBot.Graphics;

/// <summary>The Nunito faces every card is drawn with.</summary>
internal sealed class CardFonts
{
    private static readonly Lazy<CardFonts> Instance = new(() => new CardFonts());

    private CardFonts()
    {
        FontCollection fonts = new();
        using Stream bold = CardDrawing.OpenResource("Fonts/Nunito/Nunito-Bold.ttf");
        using Stream extraBold = CardDrawing.OpenResource("Fonts/Nunito/Nunito-ExtraBold.ttf");
        Bold = fonts.Add(bold);
        ExtraBold = fonts.Add(extraBold);
    }

    public static CardFonts Shared => Instance.Value;

    public FontFamily Bold { get; }
    public FontFamily ExtraBold { get; }
}

/// <summary>Colours and small drawing helpers shared by the replay, profile and score cards.</summary>
internal static class CardDrawing
{
    public static readonly Color Accent = Color.ParseHex("5FE3D6");
    public static readonly Color AccentText = Color.ParseHex("0E2A2F");
    public static readonly Color PillFill = Color.White.WithAlpha(0.11f);
    public static readonly Color SoftText = Color.ParseHex("E4ECEE");

    public static Stream OpenResource(string path)
    {
        Assembly assembly = typeof(CardDrawing).Assembly;
        string suffix = path.Replace('/', '.');
        string? name = assembly.GetManifestResourceNames()
            .FirstOrDefault(resource => resource.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
        return (name is null ? null : assembly.GetManifestResourceStream(name))
               ?? throw new FileNotFoundException($"Embedded resource '{path}' was not found.", path);
    }

    /// <summary>
    /// The icon of a game mode, centred on a point in pixels. Images ignore the drawing transform, so a caller that
    /// uses one passes the position and the size already transformed.
    /// </summary>
    public static void DrawModeIcon(IImageProcessingContext context, OsuGameMode mode, PointF center, float size)
    {
        string suffix = mode switch
        {
            OsuGameMode.Taiko => "taiko",
            OsuGameMode.Catch => "ctb",
            OsuGameMode.Mania => "mania",
            _ => "std"
        };
        using Stream resource = OpenResource($"Assets/OsuCard/Icon/bancho_{suffix}.png");
        using Image<Rgba32> icon = SafeImageDecoding.Load(resource);

        int pixels = Math.Max(1, (int)MathF.Round(size));
        icon.Mutate(image => image.Resize(new ResizeOptions { Size = new Size(pixels, pixels), Mode = ResizeMode.Max }));
        context.DrawImage(icon, new Point((int)MathF.Round(center.X - icon.Width / 2f),
            (int)MathF.Round(center.Y - icon.Height / 2f)), 1);
    }

    /// <summary>Draws a single line of text and returns its width.</summary>
    public static float DrawText(IImageProcessingContext context, string text, Font font, Color color, float x,
        float y, VerticalAlignment alignment = VerticalAlignment.Top)
    {
        if (text.Length == 0)
            return 0;

        context.DrawText(new RichTextOptions(font)
        {
            Origin = new PointF(x, y),
            VerticalAlignment = alignment,
            KerningMode = KerningMode.Standard
        }, text, color);
        return Measure(text, font).Width;
    }

    public static FontRectangle Measure(string text, Font font) =>
        TextMeasurer.MeasureAdvance(text, new TextOptions(font) { KerningMode = KerningMode.Standard });

    public static Font Fit(string text, FontFamily family, float size, float minimumSize, float maximumWidth)
    {
        for (; size > minimumSize; size -= 1)
        {
            Font font = family.CreateFont(size);
            if (Measure(text, font).Width <= maximumWidth)
                return font;
        }

        return family.CreateFont(minimumSize);
    }

    /// <summary>The text cut with "…" to fit <paramref name="maximumWidth" />.</summary>
    public static string Ellipsize(string text, Font font, float maximumWidth)
    {
        if (Measure(text, font).Width <= maximumWidth)
            return text;

        for (int length = text.Length - 1; length > 0; length--)
        {
            string candidate = text[..length].TrimEnd() + "…";
            if (Measure(candidate, font).Width <= maximumWidth)
                return candidate;
        }

        return "…";
    }

    public static IPath RoundedRectangle(float x, float y, float width, float height, float radius)
    {
        radius = Math.Min(radius, Math.Min(width, height) / 2);
        var points = new List<PointF>();
        AddCorner(x + width - radius, y + radius, -90);
        AddCorner(x + width - radius, y + height - radius, 0);
        AddCorner(x + radius, y + height - radius, 90);
        AddCorner(x + radius, y + radius, 180);
        return new Polygon(new LinearLineSegment(points.ToArray()));

        void AddCorner(float centerX, float centerY, float startDegrees)
        {
            for (var step = 0; step <= 8; step++)
            {
                double angle = (startDegrees + step * 90 / 8.0) * Math.PI / 180;
                points.Add(new PointF(centerX + radius * (float)Math.Cos(angle),
                    centerY + radius * (float)Math.Sin(angle)));
            }
        }
    }

    public static Color Lighten(Color color, float amount)
    {
        Rgba32 pixel = color.ToPixel<Rgba32>();
        return Color.FromRgb(
            (byte)(pixel.R + (255 - pixel.R) * amount),
            (byte)(pixel.G + (255 - pixel.G) * amount),
            (byte)(pixel.B + (255 - pixel.B) * amount));
    }

    public static string Number(double value, string format) => value.ToString(format, CultureInfo.InvariantCulture);

    public static string Pp(double? pp) => pp is { } value && double.IsFinite(value) ? Number(value, "0.00") : "—";

    /// <summary>The first frame of an image; <c>null</c> when it is missing or unreadable.</summary>
    public static Image<Rgba32>? TryLoad(byte[]? bytes)
    {
        if (bytes is not { Length: > 0 })
            return null;

        try
        {
            using Image<Rgba32> image = SafeImageDecoding.Load(bytes);
            return image.Frames.CloneFrame(0);
        }
        catch (Exception exception) when (exception is UnknownImageFormatException or InvalidImageContentException)
        {
            return null;
        }
    }
}
