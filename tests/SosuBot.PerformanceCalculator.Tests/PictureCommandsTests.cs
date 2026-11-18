using System.Text;
using OsuApi.BanchoV2.Models;
using OsuApi.BanchoV2.Users.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SosuBot.Database.Models;
using SosuBot.Graphics;
using SosuBot.Graphics.Models;
using SosuBot.Helpers;
using SosuBot.Helpers.OutputText;
using SosuBot.Services;
using Telegram.Bot.Types.ReplyMarkups;
using Xunit;

namespace SosuBot.PerformanceCalculator.Tests;

/// <summary>The pictures of /userp and /scorep and the buttons under them.</summary>
public sealed class PictureCommandsTests
{
    private static ScoreCardData ScoreCard(byte[]? background) => new()
    {
        Background = background,
        Rank = "SH",
        Mods = ["HD", "DT"],
        Score = 917_561,
        MaxScore = 1_279_200,
        AccuracyPercent = 97.12,
        Combo = 2693,
        MaxCombo = 2695,
        Pp = 534.33,
        FullComboPp = 540.98,
        PerfectPp = 645.39,
        Hits = [("300", 2032), ("100", 98), ("50", 1), ("Miss", 0)]
    };

    private static byte[] Jpeg(int width, int height)
    {
        using Image<Rgba32> image = new(width, height, Color.SteelBlue.ToPixel<Rgba32>());
        using var stream = new MemoryStream();
        image.SaveAsJpeg(stream);
        return stream.ToArray();
    }

    [Theory]
    [InlineData(1920, 1080)]
    [InlineData(1366, 768)]
    [InlineData(800, 600)]
    [InlineData(1080, 1920)]
    public void TheScorePictureIsTheSizeOfTheBackground(int width, int height)
    {
        using MemoryStream picture = new ScoreCardGenerator().Generate(ScoreCard(Jpeg(width, height)));
        using Image<Rgba32> image = Image.Load<Rgba32>(picture);

        Assert.Equal(width, image.Width);
        Assert.Equal(height, image.Height);
    }

    [Fact]
    public void HugeBackgroundsAreScaledDownKeepingTheirShape()
    {
        using MemoryStream picture = new ScoreCardGenerator().Generate(ScoreCard(Jpeg(5120, 2880)));
        using Image<Rgba32> image = Image.Load<Rgba32>(picture);

        Assert.Equal(ScoreCardGenerator.MaximumSide, image.Width);
        Assert.Equal(1440, image.Height);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not an image")]
    public void AScoreWithoutABackgroundStillGetsAPicture(string? background)
    {
        using MemoryStream picture = new ScoreCardGenerator().Generate(
            ScoreCard(background is null ? null : Encoding.ASCII.GetBytes(background)));
        using Image<Rgba32> image = Image.Load<Rgba32>(picture);

        Assert.Equal(ScoreCardGenerator.FallbackWidth, image.Width);
        Assert.Equal(ScoreCardGenerator.FallbackHeight, image.Height);
    }

    [Fact]
    public void EveryModeDrawsItsOwnIcon()
    {
        string[] pictures = Enum.GetValues<OsuGameMode>().Select(mode =>
        {
            ScoreCardData card = ScoreCard(null);
            using MemoryStream picture = new ScoreCardGenerator().Generate(new ScoreCardData
            {
                Mode = mode,
                Rank = card.Rank,
                Score = card.Score,
                MaxScore = card.MaxScore
            });
            return Convert.ToHexString(picture.ToArray());
        }).ToArray();

        Assert.Equal(pictures.Length, pictures.Distinct().Count());
    }

    [Theory]
    [InlineData(Playmode.Osu, "300,100,50,Miss")]
    [InlineData(Playmode.Taiko, "300,100,Miss")]
    [InlineData(Playmode.Catch, "300,100,50,Miss")]
    [InlineData(Playmode.Mania, "320,300,200,100,50,Miss")]
    public void EveryModeShowsItsOwnHitResults(Playmode mode, string labels) =>
        Assert.Equal(labels, string.Join(',', ScoreCardService.Hits(new ScoreStatistics(), mode).Select(hit => hit.Label)));

    [Fact]
    public void MissedLargeTicksCountAsMissesInCatch() =>
        Assert.Contains(("Miss", 3), ScoreCardService.Hits(new ScoreStatistics { Miss = 2, LargeTickMiss = 1 }, Playmode.Catch));

    [Theory]
    [InlineData(OsuGameMode.Osu)]
    [InlineData(OsuGameMode.Mania)]
    public void TheProfilePictureHasTheCardSizeWithAndWithoutImages(OsuGameMode mode)
    {
        foreach (byte[]? art in new[] { null, Jpeg(400, 200), "broken"u8.ToArray() })
        {
            using MemoryStream picture = new UserProfileCardGenerator().Generate(new UserProfileCardData
            {
                Username = "Shoukko",
                CountryCode = "UZ",
                Mode = mode,
                Avatar = art,
                Banner = art,
                Pp = 7421.5,
                PpDifference = "(+12.30)"
            });
            using Image<Rgba32> image = Image.Load<Rgba32>(picture);

            Assert.Equal(UserProfileCardGenerator.Width, image.Width);
            Assert.Equal(UserProfileCardGenerator.Height, image.Height);
        }
    }

    [Fact]
    public void TheProfilePictureTakesThePlayersNumbers()
    {
        var user = new UserExtend
        {
            Username = "Shoukko",
            CountryCode = "UZ",
            JoinDate = null,
            Statistics = new UserStatistics
            {
                GlobalRank = 1234,
                Pp = 7421.5f,
                PlayTime = 7200,
                GradeCounts = new GradeCounts { SSH = 1, SS = 2, SH = 3, S = 4, A = 5 }
            }
        };

        UserProfileCardData card = UserProfileCardService.ToCardData(user, Playmode.Taiko,
            new UserProfileDifferenceTexts("(+1.00)", "", ""), null, null);

        Assert.Equal(OsuGameMode.Taiko, card.Mode);
        Assert.Equal(1234, card.GlobalRank);
        Assert.Equal(2, card.PlayTimeHours);
        Assert.Equal("(+1.00)", card.PpDifference);
        Assert.Equal((1, 2, 3, 4, 5), (card.SilverSs, card.Ss, card.SilverS, card.S, card.A));
        Assert.Null(card.Registered);
    }

    [Fact]
    public void TheShownModeIsMarkedAndTheOthersSwitch()
    {
        InlineKeyboardButton[] buttons = UserHelper.BuildUserPictureKeyboard("Some Long Name 1", Playmode.Taiko)
            .InlineKeyboard.SelectMany(row => row).ToArray();

        Assert.Equal(["Standard", "• Taiko", "Catch", "Mania"], buttons.Select(button => button.Text));
        Assert.Equal("userp 0 Some Long Name 1", buttons[0].CallbackData);
        Assert.Equal("userp shown", buttons[1].CallbackData);
        Assert.All(buttons, button => Assert.True(Encoding.UTF8.GetByteCount(button.CallbackData!) <= 64));
    }
}
