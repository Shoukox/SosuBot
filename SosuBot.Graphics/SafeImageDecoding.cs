using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;

namespace SosuBot.Graphics;

/// <summary>
///     Single entry point for decoding images. ImageSharp is pinned to 3.1.x because every patched release
///     requires a Six Labors license key; this hardening keeps the known 3.1.x advisories (see
///     Directory.Build.props) out of reach: only the formats osu! and the bundled assets use are decodable
///     (no TIFF), and metadata such as ICC profiles is never parsed.
/// </summary>
internal static class SafeImageDecoding
{
    private static readonly DecoderOptions Options = new()
    {
        Configuration = new Configuration(
            new PngConfigurationModule(),
            new JpegConfigurationModule(),
            new GifConfigurationModule(),
            new WebpConfigurationModule()),
        SkipMetadata = true
    };

    public static Image<Rgba32> Load(byte[] bytes) => Image.Load<Rgba32>(Options, bytes);

    public static Image<Rgba32> Load(Stream stream) => Image.Load<Rgba32>(Options, stream);
}
