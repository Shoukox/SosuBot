using SosuBot.Graphics;
using SosuBot.Graphics.Models;
using Xunit;

namespace SosuBot.PerformanceCalculator.Tests;

public sealed class TaikoSkillCalculatorTests
{
    [Fact]
    public void StaminaUsesNoteDensityInsteadOfNominalBpmAlone()
    {
        var calculator = new PlayerSkillCalculator();

        PlayerSkills denseLowBpm = calculator.Calculate(
            [CreateInput(bpm: 120, speedNoteCount: 600)]);
        PlayerSkills sparseHighBpm = calculator.Calculate(
            [CreateInput(bpm: 300, speedNoteCount: 100)]);

        Assert.True(denseLowBpm.Speed > sparseHighBpm.Speed);
        Assert.Equal("Stamina", denseLowBpm.GetMetrics(OsuGameMode.Taiko)[0].Label);
    }

    [Fact]
    public void TaikoAccuracyRespondsToOverallDifficulty()
    {
        var calculator = new PlayerSkillCalculator();

        PlayerSkills lowOd = calculator.Calculate([CreateInput(overallDifficulty: 0)]);
        PlayerSkills highOd = calculator.Calculate([CreateInput(overallDifficulty: 10)]);

        Assert.True(highOd.Accuracy > lowOd.Accuracy);
    }

    [Fact]
    public void TaikoBoundaryValuesRemainFinite()
    {
        var calculator = new PlayerSkillCalculator();

        PlayerSkills skills = calculator.Calculate(
        [
            CreateInput(
                bpm: 0,
                speedNoteCount: 0,
                hitCircleCount: 0,
                sliderCount: 0,
                circleSize: 0,
                drainRate: 0,
                overallDifficulty: 10)
        ]);

        Assert.True(double.IsFinite(skills.Speed));
        Assert.True(double.IsFinite(skills.Accuracy));
        Assert.True(double.IsFinite(skills.Stars));
    }

    private static PlayerScoreSkillInput CreateInput(
        double bpm = 180,
        double speedNoteCount = 0,
        int hitCircleCount = 1000,
        int sliderCount = 0,
        double circleSize = 4,
        double overallDifficulty = 5,
        double drainRate = 5)
    {
        return new PlayerScoreSkillInput
        {
            Mode = OsuGameMode.Taiko,
            StarRating = 5,
            AccuracyPercent = 100,
            Bpm = bpm,
            CircleSize = circleSize,
            ApproachRate = 0,
            OverallDifficulty = overallDifficulty,
            DrainRate = drainRate,
            Combo = 1000,
            MaximumCombo = 1000,
            HitCircleCount = hitCircleCount,
            SliderCount = sliderCount,
            HitLength = 120,
            AimDifficulty = 0,
            SpeedDifficulty = 0,
            SpeedNoteCount = speedNoteCount,
            Mods = []
        };
    }
}
