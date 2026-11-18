using Microsoft.Extensions.Logging;
using osu.Game.Beatmaps;
using OsuApi.BanchoV2.Models;
using OsuApi.BanchoV2.Users.Models;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.Graphics;
using SosuBot.Graphics.Models;
using SosuBot.PerformanceCalculator;
using GameMod = osu.Game.Rulesets.Mods.Mod;

namespace SosuBot.Services;

/// <summary>Draws the /scorep picture: the statistics of a score over the background of its beatmap.</summary>
public sealed class ScoreCardService(
    OsuProfileImageService images,
    BeatmapsService beatmapsService,
    IPerformanceCalculator performanceCalculator,
    ScoreCardGenerator generator,
    ILogger<ScoreCardService> logger)
{
    public async Task<MemoryStream> GenerateAsync(Score score, BeatmapExtended beatmap, Playmode mode,
        CancellationToken cancellationToken)
    {
        GameMod[] mods = (score.Mods ?? []).ToOsuMods(mode);
        Task<byte[]?> background = DownloadBackgroundAsync(beatmap.BeatmapsetId, cancellationToken);

        PPCalculationResult? played = null, fullCombo = null, perfect = null;
        try
        {
            await using Stream file = await beatmapsService.DownloadOrCacheBeatmapAsync(beatmap.Id!.Value,
                beatmap.Checksum, cancellationToken);

            // Unranked beatmaps have no pp on the website, so it is worked out the way osu! would.
            if (score.Pp is null)
            {
                ReplayFullScore? full = ReplayCardService.FullScoreFromOnlineScore(score, beatmap.Id!.Value,
                    score.MaxCombo ?? 0, mode);
                played = await Calculate(file, full is null ? score.Accuracy : null, score.MaxCombo, full?.Statistics, full);
            }

            double accuracy = score.Accuracy ?? played?.CalculatedAccuracy ?? 1;
            fullCombo = await Calculate(file, accuracy, null, null, null);
            perfect = await Calculate(file, 1, null, null, null);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Could not calculate pp for score {ScoreId}", score.Id);
        }

        return generator.Generate(new ScoreCardData
        {
            Background = await background,
            Mode = (OsuGameMode)mode,
            Rank = score.Rank ?? "F",
            Mods = (score.Mods ?? []).Select(mod => mod.Acronym ?? "").Where(acronym => acronym is not ("" or "CL")).ToArray(),
            Score = score.TotalScore ?? 0,
            MaxScore = MaxScore(score, beatmap, mode, mods),
            AccuracyPercent = (score.Accuracy ?? 0) * 100,
            Combo = score.MaxCombo ?? 0,
            MaxCombo = ReplayCardService.MaximumCombo(mode, score.LegacyScoreId > 0, beatmap.ModeInt,
                (fullCombo ?? played)?.BeatmapMaxCombo, beatmap.MaxCombo),
            Pp = score.Pp ?? played?.PP,
            FullComboPp = fullCombo?.PP,
            PerfectPp = perfect?.PP,
            Hits = Hits(score.Statistics ?? new ScoreStatistics(), mode)
        });

        async Task<PPCalculationResult?> Calculate(Stream file, double? accuracy, int? combo,
            Dictionary<osu.Game.Rulesets.Scoring.HitResult, int>? statistics, ReplayFullScore? full)
        {
            file.Position = 0;
            return await performanceCalculator.CalculatePpAsync(beatmap.Id!.Value, file, accuracy,
                scoreMaxCombo: combo, scoreMods: mods, scoreStatistics: statistics, rulesetId: (int)mode,
                cancellationToken: cancellationToken, maximumStatistics: full?.MaximumStatistics,
                legacyTotalScore: full?.LegacyTotalScore);
        }
    }

    /// <summary>The hit results of a score, labelled the way the replay card labels them.</summary>
    public static IReadOnlyList<(string Label, int Count)> Hits(ScoreStatistics statistics, Playmode mode) => mode switch
    {
        Playmode.Taiko => [("300", statistics.Great), ("100", statistics.Ok), ("Miss", statistics.Miss)],
        Playmode.Catch =>
        [
            ("300", statistics.Great), ("100", statistics.LargeTickHit), ("50", statistics.SmallTickHit),
            ("Miss", statistics.Miss + statistics.LargeTickMiss)
        ],
        Playmode.Mania =>
        [
            ("320", statistics.Perfect), ("300", statistics.Great), ("200", statistics.Good),
            ("100", statistics.Ok), ("50", statistics.Meh), ("Miss", statistics.Miss)
        ],
        _ => [("300", statistics.Great), ("100", statistics.Ok), ("50", statistics.Meh), ("Miss", statistics.Miss)]
    };

    /// <summary>The full-size background of the beatmapset, or its wide cover if that one is not available.</summary>
    private async Task<byte[]?> DownloadBackgroundAsync(int? beatmapsetId, CancellationToken cancellationToken)
    {
        if (beatmapsetId is not { } setId)
            return null;

        string covers = $"https://assets.ppy.sh/beatmaps/{setId}/covers";
        return await images.DownloadImageAsync($"{covers}/fullsize.jpg", cancellationToken)
               ?? await images.DownloadImageAsync($"{covers}/cover@2x.jpg", cancellationToken);
    }

    private long? MaxScore(Score score, BeatmapExtended beatmap, Playmode mode, GameMod[] mods)
    {
        try
        {
            return ReplayCardService.MaxStandardisedScore(mode, new BeatmapDifficulty
            {
                CircleSize = beatmap.CS ?? 5,
                ApproachRate = beatmap.AR ?? 5,
                OverallDifficulty = beatmap.Accuracy ?? 5,
                DrainRate = beatmap.Drain ?? 5
            }, mods);
        }
        catch (Exception exception)
        {
            // Without the maximum the bar is simply full.
            logger.LogWarning(exception, "Could not work out the maximum score of score {ScoreId}", score.Id);
            return null;
        }
    }
}
