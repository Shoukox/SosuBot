using SosuBot.Graphics.Models;
using Xunit;

namespace SosuBot.Graphics.Tests;

public sealed class OsuStandardSkillCalculatorTests
{
    private readonly OsuStandardSkillCalculator _calculator = new();

    [Fact]
    public void Calculate_UsesActualToPerfectComponentRatio()
    {
        OsuSkillRating result = _calculator.Calculate([
            CreateScore() with
            {
                ActualAimPerformance = 50,
                PerfectAimPerformance = 100,
                ActualSpeedPerformance = 80,
                PerfectSpeedPerformance = 100,
                ActualAccuracyPerformance = 90,
                PerfectAccuracyPerformance = 100
            }
        ]);

        ScoreSkillEvidence evidence = Assert.Single(result.Evidence);

        Assert.Equal(0.5, evidence.AimExecutionQuality, 10);
        Assert.Equal(0.8, evidence.SpeedExecutionQuality, 10);
        Assert.Equal(0.9, evidence.AccuracyExecutionQuality, 10);
        Assert.True(result.Aim > 0);
        Assert.True(result.Speed > 0);
        Assert.True(result.Accuracy > 0);
    }

    [Fact]
    public void Calculate_SpecialistMapsSeparateAimAndSpeed()
    {
        OsuSkillRating aimPlayer = _calculator.Calculate([
            CreateScore() with { AimDifficulty = 8, SpeedDifficulty = 1 },
            CreateScore(2) with { AimDifficulty = 7.5, SpeedDifficulty = 1.1 }
        ]);
        OsuSkillRating speedPlayer = _calculator.Calculate([
            CreateScore() with { AimDifficulty = 1, SpeedDifficulty = 8 },
            CreateScore(2) with { AimDifficulty = 1.1, SpeedDifficulty = 7.5 }
        ]);

        Assert.True(aimPlayer.Aim > aimPlayer.Speed * 1.5);
        Assert.True(speedPlayer.Speed > speedPlayer.Aim * 1.5);
    }

    [Fact]
    public void Calculate_HarderAimChallengeBeatsEasierChallengeAtSimilarExecution()
    {
        OsuSkillRating result = _calculator.Calculate([
            CreateScore() with { AimDifficulty = 8, ActualAimPerformance = 99, PerfectAimPerformance = 100 },
            CreateScore(2) with { AimDifficulty = 6, ActualAimPerformance = 99, PerfectAimPerformance = 100 }
        ]);

        ScoreSkillEvidence strongest = result.Evidence.OrderByDescending(item => item.AimEvidence).First();

        Assert.True(strongest.AimChallenge > 8);
        Assert.True(strongest.AimEvidence > result.Evidence.Min(item => item.AimEvidence));
    }

    [Fact]
    public void Calculate_ChokeOnDifficultMapStillProducesEvidence()
    {
        OsuSkillRating result = _calculator.Calculate([
            CreateScore() with
            {
                AimDifficulty = 8,
                ActualAimPerformance = 35,
                PerfectAimPerformance = 100
            }
        ]);

        ScoreSkillEvidence evidence = Assert.Single(result.Evidence);

        Assert.True(evidence.AimExecutionQuality > 0);
        Assert.True(evidence.AimEvidence > 0);
        Assert.True(result.Aim > 0);
    }

    [Fact]
    public void Calculate_HardAccuracyChallengeBeatsEasyPerfectScore()
    {
        OsuSkillRating result = _calculator.Calculate([
            CreateScore() with
            {
                BeatmapId = 1,
                PerfectAccuracyPerformance = 40,
                ActualAccuracyPerformance = 40
            },
            CreateScore(2) with
            {
                PerfectAccuracyPerformance = 200,
                ActualAccuracyPerformance = 150
            }
        ]);

        ScoreSkillEvidence easy = result.Evidence.Single(item => item.BeatmapId == 1);
        ScoreSkillEvidence hard = result.Evidence.Single(item => item.BeatmapId == 2);

        Assert.True(hard.AccuracyChallenge > easy.AccuracyChallenge);
        Assert.True(hard.AccuracyEvidence > easy.AccuracyEvidence);
    }

    [Fact]
    public void Calculate_OneExtremeScoreDoesNotCompletelyDefineRating()
    {
        OsuSkillRating stable = _calculator.Calculate(
            Enumerable.Range(1, 10).Select(id => CreateScore(id) with { AimDifficulty = 6 }));
        OsuSkillRating withOneExtreme = _calculator.Calculate(
            Enumerable.Range(1, 9).Select(id => CreateScore(id) with { AimDifficulty = 6 })
                .Append(CreateScore(10) with { AimDifficulty = 14 }));

        Assert.True(withOneExtreme.Aim > stable.Aim);
        Assert.True(withOneExtreme.Aim < stable.Aim * 2.5);
    }

    [Fact]
    public void Calculate_InvalidScoresAreSkippedAndConfidenceReflectsCoverage()
    {
        OsuSkillRating result = _calculator.Calculate([
            CreateScore(),
            CreateScore(2) with { TotalHitObjects = 0 },
            CreateScore(3) with { PerfectAimPerformance = 0 }
        ]);

        Assert.Equal(2, result.CalculatedScores);
        Assert.Equal(2, result.Evidence.Count);
        Assert.InRange(result.AimConfidence, 0, 1);
        Assert.InRange(result.SpeedConfidence, 0, 1);
        Assert.InRange(result.AccuracyConfidence, 0, 1);
    }

    [Fact]
    public void Calculate_EmptyOrNullScoresReturnNeutralLowConfidenceRating()
    {
        OsuSkillRating empty = _calculator.Calculate([]);
        OsuSkillRating withNull = _calculator.Calculate([
            null,
            CreateScore()
        ]);

        Assert.Equal(650, empty.Aim);
        Assert.Equal(650, empty.Speed);
        Assert.Equal(650, empty.Accuracy);
        Assert.Equal(0, empty.AimConfidence);
        Assert.True(withNull.Aim > 0);
        Assert.Equal(1, withNull.CalculatedScores);
    }

    [Fact]
    public void Calculate_RatingHasNo1000Ceiling()
    {
        OsuSkillRating result = _calculator.Calculate([
            CreateScore() with { AimDifficulty = 10, ActualAimPerformance = 100, PerfectAimPerformance = 100 },
            CreateScore(2) with { AimDifficulty = 10, ActualAimPerformance = 100, PerfectAimPerformance = 100 }
        ]);

        Assert.True(result.Aim > 1000);
    }

    private static OsuStandardScoreSkillInput CreateScore(int beatmapId = 1) => new()
    {
        BeatmapId = beatmapId,
        ModSignature = "NM",
        StarRating = 7,
        AimDifficulty = 6,
        AimDifficultSliderCount = 40,
        AimDifficultStrainCount = 120,
        SpeedDifficulty = 6,
        SpeedNoteCount = 300,
        SpeedDifficultStrainCount = 100,
        ReadingDifficulty = 2,
        HitCircleCount = 700,
        SliderCount = 100,
        SpinnerCount = 0,
        TotalHitObjects = 800,
        OverallDifficulty = 9,
        ApproachRate = 10,
        ClockRate = 1,
        ActualAimPerformance = 100,
        ActualSpeedPerformance = 100,
        ActualAccuracyPerformance = 100,
        PerfectAimPerformance = 100,
        PerfectSpeedPerformance = 100,
        PerfectAccuracyPerformance = 100
    };
}
