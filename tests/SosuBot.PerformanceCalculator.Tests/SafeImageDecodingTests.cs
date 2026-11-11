using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SosuBot.Graphics;
using Xunit;

namespace SosuBot.PerformanceCalculator.Tests;

public sealed class SafeImageDecodingTests
{
    [Fact]
    public void DecodesFormatsServedByOsu()
    {
        using var source = new Image<Rgba32>(4, 3, Color.Red);
        foreach (Action<Image, Stream> save in new Action<Image, Stream>[]
                 {
                     (image, stream) => image.SaveAsPng(stream),
                     (image, stream) => image.SaveAsJpeg(stream),
                     (image, stream) => image.SaveAsGif(stream),
                     (image, stream) => image.SaveAsWebp(stream)
                 })
        {
            using var stream = new MemoryStream();
            save(source, stream);

            using Image<Rgba32> decoded = SafeImageDecoding.Load(stream.ToArray());

            Assert.Equal(4, decoded.Width);
            Assert.Equal(3, decoded.Height);
        }
    }

    [Fact]
    public void RejectsTiffSoVulnerableDecoderIsUnreachable()
    {
        using var source = new Image<Rgba32>(4, 3, Color.Red);
        using var stream = new MemoryStream();
        source.SaveAsTiff(stream);

        Assert.Throws<UnknownImageFormatException>(() => SafeImageDecoding.Load(stream.ToArray()));
    }
}
