using System.Buffers.Binary;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SosuBot.Graphics.Models;
using Xunit;

namespace SosuBot.Graphics.Tests;

public sealed class PlayerSkillCalculatorTests
{
    private readonly PlayerSkillCalculator _calculator = new();

    [Fact]
    public void Calculate_OsuFormulaMatchesTinyBot()
    {
        PlayerSkills result = _calculator.Calculate([
            CreateScore(OsuGameMode.Osu) with
            {
                StarRating = 6.4,
                AimDifficulty = 3.1,
                SpeedDifficulty = 2.8,
                CircleSize = 4.2,
                Bpm = 190,
                ApproachRate = 9.5,
                AccuracyPercent = 98.2,
                Combo = 1000,
                MaximumCombo = 1200,
                OverallDifficulty = 9,
                DrainRate = 6,
                SpeedNoteCount = 250,
                Mods = ["HD"]
            }
        ]);

        AssertClose(623.0323816830773, result.Aim);
        AssertClose(606.8701630903861, result.Speed);
        AssertClose(591.8445808148624, result.Accuracy);
        AssertClose(6.4, result.Stars);
    }

    [Theory]
    [MemberData(nameof(LegacyModeFixtures))]
    public void Calculate_LegacyModeFormulasMatchTinyBot(PlayerScoreSkillInput input, PlayerSkills expected)
    {
        PlayerSkills result = _calculator.Calculate([input]);

        AssertClose(expected.Aim, result.Aim);
        AssertClose(expected.Speed, result.Speed);
        AssertClose(expected.Accuracy, result.Accuracy);
        AssertClose(expected.Stars, result.Stars);
    }

    [Fact]
    public void Calculate_UsesAtMostTopFiftyScores()
    {
        PlayerScoreSkillInput[] scores = Enumerable.Range(0, 60)
            .Select(index => CreateScore(OsuGameMode.Taiko) with { StarRating = index < 50 ? 5 : 50 })
            .ToArray();

        PlayerSkills result = _calculator.Calculate(scores);

        Assert.Equal(PlayerSkillCalculator.MaximumScoreCount, result.CalculatedScores);
        AssertClose(5, result.Stars);
    }

    [Fact]
    public void ProfileCardGenerator_ProducesExpectedPngDimensions()
    {
        ProfileCardGenerator generator = new();
        using MemoryStream card = generator.Generate(new ProfileCardData
        {
            Username = "Shoukko",
            Mode = OsuGameMode.Osu,
            Skills = new PlayerSkills(623, 607, 592, 6.4, 50),
            Avatar = null
        });
        byte[] png = card.ToArray();

        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, png[..8]);
        Assert.Equal(ProfileCardGenerator.CardWidth, BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4)));
        Assert.Equal(ProfileCardGenerator.CardHeight, BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4)));

        using Image<Rgba32> decoded = Image.Load<Rgba32>(png);
        Assert.Equal(255, decoded[ProfileCardGenerator.CardWidth / 2, 0].A);
        Assert.Equal(255, decoded[0, ProfileCardGenerator.CardHeight / 2].A);
    }

    [Fact]
    public void ProfileCardGenerator_RendersEveryGameMode()
    {
        ProfileCardGenerator generator = new();

        foreach (OsuGameMode mode in Enum.GetValues<OsuGameMode>())
        {
            using MemoryStream card = generator.Generate(new ProfileCardData
            {
                Username = "Shoukko",
                Mode = mode,
                Skills = new PlayerSkills(623, 607, 592, 6.4, 50),
                Avatar = null
            });
            using Image<Rgba32> decoded = Image.Load<Rgba32>(card.ToArray());

            Assert.Equal(ProfileCardGenerator.CardWidth, decoded.Width);
            Assert.Equal(ProfileCardGenerator.CardHeight, decoded.Height);
        }
    }

    [Fact]
    public void PlayerSkills_UsesModeSpecificMetrics()
    {
        PlayerSkills skills = new(623, 607, 592, 6.4, 50);

        Assert.Equal(["Aim", "Speed", "Accuracy"],
            skills.GetMetrics(OsuGameMode.Osu).Select(metric => metric.Label));
        Assert.Equal(["Speed", "Accuracy"],
            skills.GetMetrics(OsuGameMode.Taiko).Select(metric => metric.Label));
        Assert.Equal(["Aim", "Accuracy"],
            skills.GetMetrics(OsuGameMode.Catch).Select(metric => metric.Label));
        Assert.Equal(["Finger Control", "Speed", "Accuracy"],
            skills.GetMetrics(OsuGameMode.Mania).Select(metric => metric.Label));
    }

    public static TheoryData<PlayerScoreSkillInput, PlayerSkills> LegacyModeFixtures => new()
    {
        {
            CreateScore(OsuGameMode.Taiko) with
            {
                StarRating = 5.5,
                Bpm = 220,
                AccuracyPercent = 97.5,
                OverallDifficulty = 8.5,
                DrainRate = 6
            },
            new PlayerSkills(0, 652.9493278233529, 531.0454388903829, 5.5, 1)
        },
        {
            CreateScore(OsuGameMode.Catch) with
            {
                StarRating = 5,
                Bpm = 180,
                AccuracyPercent = 99,
                OverallDifficulty = 8,
                DrainRate = 5,
                CircleSize = 4
            },
            new PlayerSkills(614.021977990813, 0, 555.6356899660542, 5, 1)
        },
        {
            CreateScore(OsuGameMode.Mania) with
            {
                StarRating = 4.8,
                Bpm = 200,
                AccuracyPercent = 96,
                OverallDifficulty = 8,
                DrainRate = 7,
                CircleSize = 4,
                HitCircleCount = 500,
                SliderCount = 100
            },
            new PlayerSkills(553.0180181351359, 338.3833333742823, 450.1361388897904, 4.8, 1)
        }
    };

    private static PlayerScoreSkillInput CreateScore(OsuGameMode mode) => new()
    {
        Mode = mode,
        StarRating = 5,
        AccuracyPercent = 98,
        Bpm = 180,
        CircleSize = 4,
        ApproachRate = 9,
        OverallDifficulty = 8,
        DrainRate = 6,
        Combo = 1000,
        MaximumCombo = 1000,
        HitCircleCount = 500,
        SliderCount = 100,
        AimDifficulty = 2.5,
        SpeedDifficulty = 2.5,
        SpeedNoteCount = 200,
        Mods = []
    };

    private static void AssertClose(double expected, double actual) =>
        Assert.InRange(Math.Abs(expected - actual), 0, 0.000_000_1);
}
