using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace SosuBot.Graphics;

/// <summary>
///     Turns osu! profile images into <c>data:</c> URLs small enough for a Telegram widget, which has no network
///     access and must stay under 4096 bytes after zlib compression (https://core.telegram.org/bots/widgets).
/// </summary>
public static class WidgetArtwork
{
    // From sharpest (2x the 44 px avatar circle) to smallest; the caller takes the first one that fits.
    private static readonly (int Size, int Quality)[] AvatarSteps =
        [(88, 70), (80, 60), (72, 55), (64, 50), (56, 45), (48, 40), (40, 35)];

    // The banner is shown blurred, so a thumbnail upscaled by the client is enough and costs a few hundred bytes.
    private const int BannerWidth = 48;
    private const int BannerQuality = 40;

    /// <summary>Avatar encodings from best to most compact; empty if the image cannot be decoded.</summary>
    public static IEnumerable<string> EncodeAvatarCandidates(byte[]? avatar)
    {
        using Image<Rgba32>? source = TryLoadFirstFrame(avatar);
        if (source is null)
            yield break;

        foreach ((int size, int quality) in AvatarSteps)
        {
            using Image<Rgba32> resized = source.Clone(context => context.Resize(new ResizeOptions
            {
                Size = new Size(size, size),
                Mode = ResizeMode.Crop
            }));
            yield return ToWebpDataUrl(resized, quality);
        }
    }

    /// <summary>A tiny pre-blurred banner, or <c>null</c> if the image cannot be decoded.</summary>
    public static string? EncodeBanner(byte[]? banner)
    {
        using Image<Rgba32>? source = TryLoadFirstFrame(banner);
        if (source is null)
            return null;

        int height = Math.Max(1, (int)Math.Round(BannerWidth * (double)source.Height / source.Width));
        source.Mutate(context => context.Resize(BannerWidth, height).GaussianBlur(0.8f));
        return ToWebpDataUrl(source, BannerQuality);
    }

    /// <summary>
    ///     A vivid colour of the avatar as two hex colours for a gradient (accent, deep shade), both dark enough
    ///     for white text; <c>null</c> if the image cannot be decoded.
    /// </summary>
    public static (string Accent, string Shade)? GetAccentColors(byte[]? avatar)
    {
        using Image<Rgba32>? source = TryLoadFirstFrame(avatar);
        if (source is null)
            return null;

        source.Mutate(context => context.Resize(16, 16));

        // Weight every pixel by how colourful and bright it is, so grey backgrounds do not wash the accent out.
        double red = 0, green = 0, blue = 0, total = 0;
        source.ProcessPixelRows(rows =>
        {
            for (var y = 0; y < rows.Height; y++)
            {
                foreach (Rgba32 pixel in rows.GetRowSpan(y))
                {
                    double max = Math.Max(pixel.R, Math.Max(pixel.G, pixel.B)) / 255.0;
                    double min = Math.Min(pixel.R, Math.Min(pixel.G, pixel.B)) / 255.0;
                    double saturation = max == 0 ? 0 : (max - min) / max;
                    double weight = 0.05 + saturation * saturation * max;
                    red += pixel.R * weight;
                    green += pixel.G * weight;
                    blue += pixel.B * weight;
                    total += weight;
                }
            }
        });

        var average = new Rgb24((byte)(red / total), (byte)(green / total), (byte)(blue / total));
        return (ToHex(WithBrightness(average, 0.55)), ToHex(WithBrightness(average, 0.22)));
    }

    private static Image<Rgba32>? TryLoadFirstFrame(byte[]? bytes)
    {
        if (bytes is not { Length: > 0 })
            return null;

        try
        {
            using Image<Rgba32> image = SafeImageDecoding.Load(bytes);
            // Animated GIF avatars become a still of their first frame.
            return image.Frames.CloneFrame(0);
        }
        catch (Exception exception) when (exception is UnknownImageFormatException or InvalidImageContentException)
        {
            return null;
        }
    }

    private static string ToWebpDataUrl(Image<Rgba32> image, int quality)
    {
        using var stream = new MemoryStream();
        image.SaveAsWebp(stream, new WebpEncoder
        {
            FileFormat = WebpFileFormatType.Lossy,
            Quality = quality,
            SkipMetadata = true
        });
        return "data:image/webp;base64," + Convert.ToBase64String(stream.GetBuffer(), 0, (int)stream.Length);
    }

    // Scales the colour so its brightest channel equals the target, keeping the hue.
    private static Rgb24 WithBrightness(Rgb24 color, double brightness)
    {
        double max = Math.Max(color.R, Math.Max(color.G, color.B)) / 255.0;
        if (max == 0)
            return new Rgb24((byte)(brightness * 255), (byte)(brightness * 255), (byte)(brightness * 255));

        double scale = brightness / max;
        return new Rgb24(
            (byte)Math.Clamp(color.R * scale, 0, 255),
            (byte)Math.Clamp(color.G * scale, 0, 255),
            (byte)Math.Clamp(color.B * scale, 0, 255));
    }

    private static string ToHex(Rgb24 color) => $"#{color.R:x2}{color.G:x2}{color.B:x2}";
}
