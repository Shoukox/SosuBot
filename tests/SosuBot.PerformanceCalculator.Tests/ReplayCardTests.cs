using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;
using OsuParsers.Enums;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SosuBot.Graphics;
using SosuBot.Graphics.Models;
using SosuBot.Extensions;
using SosuBot.Services;
using Xunit;

namespace SosuBot.PerformanceCalculator.Tests;

/// <summary>The card a replay file is answered with, and how a replay's mods are read.</summary>
public sealed class ReplayCardTests
{
    private static readonly osu.Game.Beatmaps.BeatmapDifficulty Difficulty = new()
    {
        CircleSize = 4, ApproachRate = 9, OverallDifficulty = 8, DrainRate = 5
    };

    public static ReplayCardData Sample(OsuGameMode mode = OsuGameMode.Osu, byte[]? background = null) => new()
    {
        Title = "Leave The Lights On (KROT Remix) (Cut Ver.)",
        Artist = "Meiko",
        DifficultyName = "Flowziee's Insane",
        Mapper = "JayBone",
        Status = "ranked",
        Mode = mode,
        Background = background,
        LengthSeconds = 103,
        MaxCombo = 512,
        Bpm = 170,
        CircleSize = mode == OsuGameMode.Mania ? 7 : 4,
        ApproachRate = 8.7,
        Drain = 5,
        OverallDifficulty = 8,
        StarRating = 4.41,
        Mods = ["HD", "DT"],
        Score = 662_293,
        MaxScore = 1_000_000,
        AccuracyPercent = 93.15,
        Combo = 362,
        Pp = 54.93,
        FullComboPp = 86.53,
        PerfectPp = 143.72,
        Hits = [("300", 312), ("100", 31), ("50", 2), ("Miss", 7)]
    };

    [Theory]
    [InlineData(OsuGameMode.Osu)]
    [InlineData(OsuGameMode.Taiko)]
    [InlineData(OsuGameMode.Catch)]
    [InlineData(OsuGameMode.Mania)]
    public void TheCardIsAPngOfTheCardSize(OsuGameMode mode)
    {
        using MemoryStream png = new ReplayCardGenerator().Generate(Sample(mode));
        using Image<Rgba32> image = Image.Load<Rgba32>(png);

        Assert.Equal(ReplayCardGenerator.Width, image.Width);
        Assert.Equal(ReplayCardGenerator.Height, image.Height);
    }

    [Fact]
    public void LongTextsBrokenCoversAndMissingPpDoNotBreakTheCard()
    {
        ReplayCardData data = Sample(background: "not an image"u8.ToArray());
        data = new ReplayCardData
        {
            Title = string.Concat(Enumerable.Repeat("Very Long Title ", 20)),
            Artist = string.Concat(Enumerable.Repeat("Артист ", 30)),
            DifficultyName = string.Concat(Enumerable.Repeat("Extra ", 20)),
            Mapper = "someone with a long name",
            Status = "graveyard",
            Background = data.Background,
            Mods = ["HD", "HR", "DT", "FL", "SD", "NF", "EZ", "HT", "SO", "TD"],
            StarRating = 12.5,
            Hits = data.Hits
        };

        using MemoryStream png = new ReplayCardGenerator().Generate(data);

        Assert.True(png.Length > 0);
    }

    [Theory]
    [InlineData("", 1_000_000)]
    [InlineData("CL", 1_000_000)]
    public void NoModReplaysTopOutAtAMillion(string mods, long expected)
    {
        mods.TryParseMods(Database.Models.Playmode.Osu, out osu.Game.Rulesets.Mods.Mod[] parsed);
        Assert.Equal(expected, ReplayCardService.MaxStandardisedScore(Database.Models.Playmode.Osu, Difficulty, parsed));
    }

    [Fact]
    public void ModsMoveTheMaximumScoreByTheirMultipliers()
    {
        "HD".TryParseMods(Database.Models.Playmode.Osu, out osu.Game.Rulesets.Mods.Mod[] hidden);
        "EZ".TryParseMods(Database.Models.Playmode.Osu, out osu.Game.Rulesets.Mods.Mod[] easy);

        Assert.True(ReplayCardService.MaxStandardisedScore(Database.Models.Playmode.Osu, Difficulty, hidden) > 1_000_000);
        Assert.True(ReplayCardService.MaxStandardisedScore(Database.Models.Playmode.Osu, Difficulty, easy) < 1_000_000);
    }

    [Theory]
    [InlineData(Database.Models.Playmode.Mania, true, 3, 5597, 5834, 5834)] // stable long notes give more combo
    [InlineData(Database.Models.Playmode.Mania, false, 3, 5597, 5834, 5597)] // lazer long notes give exactly 2
    [InlineData(Database.Models.Playmode.Mania, true, 0, 900, 700, 900)] // the API's combo is for the osu! beatmap
    [InlineData(Database.Models.Playmode.Mania, true, 3, 5597, null, 5597)]
    [InlineData(Database.Models.Playmode.Osu, true, 0, 2695, 2695, 2695)]
    [InlineData(Database.Models.Playmode.Osu, false, 0, null, 2695, 2695)]
    public void MaximumComboFollowsTheClientThatSetThePlay(Database.Models.Playmode mode, bool stable, int beatmapMode,
        int? calculated, int? api, int expected) =>
        Assert.Equal(expected, ReplayCardService.MaximumCombo(mode, stable, beatmapMode, calculated, api));

    [Fact]
    public void FailedLazerPlaysStayAnEstimate()
    {
        var passed = new LegacyReplaySoloScoreInfo
        {
            Rank = ScoreRank.A,
            Statistics = new Dictionary<HitResult, int> { [HitResult.Great] = 10 },
            MaximumStatistics = new Dictionary<HitResult, int> { [HitResult.Great] = 10 }
        };
        var failed = new LegacyReplaySoloScoreInfo
        {
            Rank = ScoreRank.F,
            Statistics = passed.Statistics,
            MaximumStatistics = passed.MaximumStatistics
        };

        Assert.NotNull(ReplayCardService.FullScoreFromLazerReplay(passed, Database.Models.Playmode.Osu));
        Assert.Null(ReplayCardService.FullScoreFromLazerReplay(failed, Database.Models.Playmode.Osu));
        Assert.Null(ReplayCardService.FullScoreFromLazerReplay(null, Database.Models.Playmode.Osu));
    }

    [Theory]
    [InlineData(5064561, 2130, true)]
    [InlineData(1, 2130, false)] // another beatmap
    [InlineData(5064561, 7, false)] // another play
    public void AnOnlineScoreMustBeTheSamePlayAsTheReplay(int beatmapId, int combo, bool matches)
    {
        var score = new OsuApi.BanchoV2.Models.Score
        {
            BeatmapId = 5064561,
            MaxCombo = 2130,
            Passed = true,
            LegacyTotalScore = 196948278,
            Statistics = new OsuApi.BanchoV2.Models.ScoreStatistics { Great = 2130, Ok = 1 },
            MaximumStatistics = new OsuApi.BanchoV2.Models.ScoreStatistics { Great = 2131 },
            Mods = [new OsuApi.BanchoV2.Models.Mod { Acronym = "CL" }]
        };

        ReplayFullScore? fullScore = ReplayCardService.FullScoreFromOnlineScore(score, beatmapId, combo,
            Database.Models.Playmode.Osu);

        Assert.Equal(matches, fullScore is not null);
        if (matches)
        {
            Assert.Equal(196948278, fullScore!.LegacyTotalScore);
            Assert.Equal("CL", Assert.Single(fullScore.Mods).Acronym);
        }
    }

    [Fact]
    public void ReplayModsAreReadAsPlayersWriteThem()
    {
        Assert.Equal(["HD", "NC"], ReplayCardService.Acronyms(Mods.Hidden | Mods.DoubleTime | Mods.Nightcore));
        Assert.Equal(["HR", "PF"], ReplayCardService.Acronyms(Mods.HardRock | Mods.SuddenDeath | Mods.Perfect));
        Assert.Empty(ReplayCardService.Acronyms(Mods.None));
    }

    [Theory]
    [InlineData("replay.osr", true)]
    [InlineData("Guest playing Song.OSR", true)]
    [InlineData("skin.osk", false)]
    [InlineData(null, false)]
    public void OnlyOsrFilesAreReplays(string? fileName, bool expected) =>
        Assert.Equal(expected, ReplayCardService.IsReplayFile(
            fileName is null ? null : new Telegram.Bot.Types.Document { FileName = fileName, FileId = "x", FileUniqueId = "x" }));
}
