using System.IO.Compression;
using System.Text;
using OsuApi.BanchoV2.Users.Models;
using SosuBot.Database.Models;
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
    public void WidgetHtmlFitsTelegramCompressedLimit(ILocalization language)
    {
        string html = UserProfileWidget.BuildHtml(language);
        byte[] bytes = Encoding.UTF8.GetBytes(html);

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
            zlib.Write(bytes);

        Assert.DoesNotContain("__L__", html);
        Assert.True(bytes.Length <= 32768, $"{bytes.Length} bytes of HTML");
        // Telegram counts zlib level 9; keep a margin for zlib implementation differences.
        Assert.True(compressed.Length <= 3800, $"{compressed.Length} bytes after zlib");
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
