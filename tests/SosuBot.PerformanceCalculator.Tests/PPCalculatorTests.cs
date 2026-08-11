using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.Osu.Mods;
using SosuBot.PerformanceCalculator;
using Xunit;

namespace SosuBot.PerformanceCalculator.Tests;

public sealed class PPCalculatorTests
{
    private static readonly byte[] Beatmap = File.ReadAllBytes(
        Path.Combine(AppContext.BaseDirectory, "testdata", "native-fixture.osu"));

    [Fact]
    public async Task CalculatesMapPreviewThroughRefactoredFacade()
    {
        IPerformanceCalculator calculator = new PPCalculator();

        PPCalculationResult? result = await calculator.CalculatePpAsync(
            beatmapId: 1,
            beatmapFile: new MemoryStream(Beatmap, writable: false),
            accuracy: 1,
            cancellationToken: CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result!.PP > 0);
        Assert.Equal(1, result.CalculatedAccuracy);
        Assert.Equal(result.BeatmapHitObjectsCount, result.ScoreHitResultsCount);
    }

    [Fact]
    public async Task LimitsFailedCalculationToPassedHitResults()
    {
        var calculator = new PPCalculator();

        PPCalculationResult? result = await calculator.CalculatePpAsync(
            beatmapId: 2,
            beatmapFile: new MemoryStream(Beatmap, writable: false),
            accuracy: 1,
            passed: false,
            scoreMaxCombo: 10,
            scoreStatistics: new Dictionary<HitResult, int>
            {
                [HitResult.Great] = 10,
                [HitResult.Miss] = 0
            },
            cancellationToken: CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(10, result!.BeatmapHitObjectsCount);
        Assert.Equal(10, result.ScoreHitResultsCount);
        Assert.True(result.PP > 0);
    }

    [Fact]
    public void CacheKeyIncludesSettingsOmittedFromSettingDescription()
    {
        var defaultDifficultyAdjust = new OsuModDifficultyAdjust();
        var extendedDifficultyAdjust = new OsuModDifficultyAdjust();
        extendedDifficultyAdjust.ExtendedLimits.Value = true;

        string defaultKey = PPCalculationCacheKey.Create(1, 0, null, [defaultDifficultyAdjust]);
        string extendedKey = PPCalculationCacheKey.Create(1, 0, null, [extendedDifficultyAdjust]);

        Assert.NotEqual(defaultKey, extendedKey);
    }
}
