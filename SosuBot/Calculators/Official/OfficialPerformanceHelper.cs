using Microsoft.Extensions.Logging;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using OsuApi.BanchoV2.Models;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.PerformanceCalculator;
using GameMod = osu.Game.Rulesets.Mods.Mod;

namespace SosuBot.Calculators.Official;

/// <summary>
/// Maps osu! API scores to the official ruleset calculator.
/// </summary>
public sealed class OfficialPerformanceHelper(
    IPerformanceCalculator performanceCalculator,
    ILogger<OfficialPerformanceHelper> logger)
{
    public async Task<OfficialScoreCalculation> CalculateScoreAsync(
        Stream beatmapFile,
        Score score,
        Playmode playmode,
        bool calculateCurrent = true,
        bool calculatePerfect = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(beatmapFile);
        ArgumentNullException.ThrowIfNull(score);

        if (score.BeatmapId is null)
            return new OfficialScoreCalculation(null, null);

        try
        {
            GameMod[] mods = (score.Mods ?? []).ToOsuMods(playmode);
            Dictionary<HitResult, int>? statistics = score.Statistics is null
                ? null
                : ToHitStatistics(score.Statistics, playmode);
            bool passed = score.Passed != false;

            PPCalculationResult? current = calculateCurrent
                ? await CalculateAsync(
                    beatmapFile,
                    score.BeatmapId.Value,
                    score.Accuracy,
                    passed,
                    score.MaxCombo,
                    mods,
                    statistics,
                    playmode,
                    cancellationToken)
                : null;

            PPCalculationResult? ifFc = null;
            if (!calculatePerfect)
            {
                double fcAccuracy = playmode is Playmode.Mania or Playmode.Taiko
                    ? 1
                    : score.Accuracy ?? current?.CalculatedAccuracy ?? 1;
                ifFc = await CalculateAsync(
                    beatmapFile,
                    score.BeatmapId.Value,
                    fcAccuracy,
                    passed: true,
                    scoreMaxCombo: null,
                    mods,
                    scoreStatistics: null,
                    playmode,
                    cancellationToken);
            }

            PPCalculationResult? perfect = calculatePerfect
                ? await CalculateAsync(
                    beatmapFile,
                    score.BeatmapId.Value,
                    accuracy: 1,
                    passed: true,
                    scoreMaxCombo: null,
                    mods,
                    scoreStatistics: null,
                    playmode,
                    cancellationToken)
                : null;

            return new OfficialScoreCalculation(current, ifFc, perfect);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not calculate official pp for score {ScoreId}", score.Id);
            return new OfficialScoreCalculation(null, null);
        }
    }

    public static double GetClockRate(IEnumerable<GameMod>? mods)
    {
        double? rate = mods?
            .OfType<ModRateAdjust>()
            .Select(mod => (double?)mod.SpeedChange.Value)
            .FirstOrDefault();

        return rate is > 0 && double.IsFinite(rate.Value) ? rate.Value : 1;
    }

    private async Task<PPCalculationResult?> CalculateAsync(
        Stream beatmapFile,
        int beatmapId,
        double? accuracy,
        bool passed,
        int? scoreMaxCombo,
        GameMod[] mods,
        Dictionary<HitResult, int>? scoreStatistics,
        Playmode playmode,
        CancellationToken cancellationToken)
    {
        return await performanceCalculator.CalculatePpAsync(
            beatmapId,
            beatmapFile,
            accuracy,
            passed,
            scoreMaxCombo,
            mods,
            scoreStatistics,
            (int)playmode,
            cancellationToken);
    }

    private static Dictionary<HitResult, int> ToHitStatistics(ScoreStatistics statistics, Playmode playmode)
    {
        return playmode switch
        {
            Playmode.Osu => new Dictionary<HitResult, int>
            {
                [HitResult.Great] = statistics.Great,
                [HitResult.Ok] = statistics.Ok,
                [HitResult.Meh] = statistics.Meh,
                [HitResult.Miss] = statistics.Miss,
                [HitResult.LargeTickMiss] = statistics.LargeTickMiss,
                [HitResult.SliderTailHit] = statistics.SliderTailHit
            },
            Playmode.Taiko => new Dictionary<HitResult, int>
            {
                [HitResult.Great] = statistics.Great,
                [HitResult.Ok] = statistics.Ok,
                [HitResult.Miss] = statistics.Miss
            },
            Playmode.Catch => new Dictionary<HitResult, int>
            {
                [HitResult.Great] = statistics.Great,
                [HitResult.Good] = statistics.LargeTickHit,
                [HitResult.Meh] = statistics.SmallTickHit,
                [HitResult.Miss] = statistics.Miss
            },
            Playmode.Mania => new Dictionary<HitResult, int>
            {
                [HitResult.Perfect] = statistics.Perfect,
                [HitResult.Great] = statistics.Great,
                [HitResult.Good] = statistics.Good,
                [HitResult.Ok] = statistics.Ok,
                [HitResult.Meh] = statistics.Meh,
                [HitResult.Miss] = statistics.Miss
            },
            _ => throw new ArgumentOutOfRangeException(nameof(playmode), playmode, "Unsupported game mode.")
        };
    }
}

public sealed record OfficialScoreCalculation(
    PPCalculationResult? Current,
    PPCalculationResult? IfFc,
    PPCalculationResult? Perfect = null);
