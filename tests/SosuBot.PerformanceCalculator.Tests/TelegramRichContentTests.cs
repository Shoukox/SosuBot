using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Text;
using OsuApi.BanchoV2.Users.Models;
using SosuBot.Database.Models;
using SosuBot.Graphics;
using SosuBot.Helpers.OutputText;
using SosuBot.Localization;
using SosuBot.Localization.Languages;
using Xunit;

namespace SosuBot.PerformanceCalculator.Tests;

/// <summary>Guards the Telegram limits of the /userw widget and the /lw rich message.</summary>
public sealed class TelegramRichContentTests
{
    public static TheoryData<ILocalization> Languages => new() { new Russian(), new English(), new Deutsch() };

    [Theory]
    [MemberData(nameof(Languages))]
    public void WidgetWithProfileImagesFitsTelegramCompressedLimit(ILocalization language)
    {
        // Noise barely compresses: the worst case for the embedded avatar and banner.
        byte[] avatar = NoisePng(256, 256);
        byte[] banner = NoisePng(1800, 500);

        string html = UserProfileWidget.BuildHtml(
            language,
            WidgetArtwork.EncodeAvatarCandidates(avatar),
            WidgetArtwork.EncodeBanner(banner),
            WidgetArtwork.GetAccentColors(avatar));

        Assert.DoesNotContain("__", html);
        Assert.True(Encoding.UTF8.GetByteCount(html) <= 32768);
        Assert.True(UserProfileWidget.CompressedSize(html) <= UserProfileWidget.MaxCompressedHtmlBytes,
            $"{UserProfileWidget.CompressedSize(html)} bytes after zlib");
        // Even the worst case keeps a real avatar and the banner background.
        Assert.Equal(2, CountOccurrences(html, "data:image/webp;base64,"));
    }

    [Fact]
    public void WidgetWithoutImagesFallsBackToInitialAndThemeColors()
    {
        string html = UserProfileWidget.BuildHtml(new Russian(), [], null, null);

        Assert.Contains("A='',B='',K=''", html);
        Assert.True(UserProfileWidget.CompressedSize(html) <= UserProfileWidget.MaxCompressedHtmlBytes);
    }

    [Fact]
    public void AccentColorsAreDarkEnoughForWhiteText()
    {
        using var bright = new Image<Rgba32>(32, 32, Color.Yellow);
        using var stream = new MemoryStream();
        bright.SaveAsPng(stream);

        (string accent, string shade) = WidgetArtwork.GetAccentColors(stream.ToArray())!.Value;

        Assert.Equal("#8c8c00", accent);
        Assert.Equal("#383800", shade);
    }

    private static byte[] NoisePng(int width, int height)
    {
        var random = new Random(42);
        using var image = new Image<Rgba32>(width, height);
        image.ProcessPixelRows(rows =>
        {
            for (var y = 0; y < rows.Height; y++)
            foreach (ref Rgba32 pixel in rows.GetRowSpan(y))
                pixel = new Rgba32((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256));
        });
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        return stream.ToArray();
    }

    [Fact]
    public void WidgetDataFitsTelegramStateLimitInTheWorstCase()
    {
        var statistics = new UserStatistics
        {
            GlobalRank = 9_999_999,
            CountryRank = 9_999_999,
            Pp = 99_999.99f,
            HitAccuracy = 99.99f,
            PlayCount = 99_999_999,
            PlayTime = int.MaxValue,
            MaximumCombo = 99_999,
            GradeCounts = new GradeCounts { SSH = 99_999, SS = 99_999, SH = 99_999, S = 99_999, A = 99_999 },
            Level = new Level { Current = 140, Progress = 99 }
        };
        var user = new UserExtend
        {
            Id = int.MaxValue,
            Username = new string('W', 15),
            CountryCode = "AU",
            Statistics = statistics
        };

        string data = UserProfileWidget.BuildPublicData(
            user,
            Playmode.Osu,
            [statistics, statistics, statistics, statistics],
            new UserProfileDifferenceTexts("(+99999.99)", "(+99.99)", "(+99999h 59m)"));

        Assert.True(Encoding.UTF8.GetByteCount(data) <= UserProfileWidget.MaxDataBytes, data);
        Assert.Contains("\"d\":[", data);
    }

    [Fact]
    public void RichLastScoresStayWithinRichMessageLimits()
    {
        LastScoreEntry entry = new(
            12, "🟡S", 123, 456, "Very &amp; long title " + new string('x', 80), "Insane", "Ranked", "7.12",
            "1234/56/7", 3, "98.76", "HDDTHR", 1234, "2345", "512.34", "600.00pp if 99.50% FC",
            "https://osu.ppy.sh/scores/1", "5 minutes ago", "100.0", false);
        LastScoreEntry[] entries = Enumerable.Repeat(entry, RichLastScores.MaxScores).ToArray();

        string html = RichLastScores.BuildHtml(new Russian(), 1, "player", "osu!standard", entries);

        Assert.True(html.Length <= 32768, $"{html.Length} characters");
        Assert.Equal(RichLastScores.MaxScores, CountOccurrences(html, "<img "));
        Assert.Contains("https://assets.ppy.sh/beatmaps/456/covers/", html);
        Assert.Equal(CountOccurrences(html, "<figure>"), CountOccurrences(html, "</figure>"));
        Assert.Equal(CountOccurrences(html, "<table "), CountOccurrences(html, "</table>"));
    }

    private static int CountOccurrences(string text, string value) =>
        (text.Length - text.Replace(value, "").Length) / value.Length;
}
