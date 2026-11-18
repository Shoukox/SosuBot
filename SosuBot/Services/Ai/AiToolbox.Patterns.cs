using System.Text.Json;
using OsuApi.BanchoV2.Users.Models;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.PerformanceCalculator;
using GameMod = osu.Game.Rulesets.Mods.Mod;
using OfficialOsuDifficulty = osu.Game.Rulesets.Osu.Difficulty.OsuDifficultyAttributes;

namespace SosuBot.Services.Ai;

// What a beatmap looks like inside: its file from the beatmap cache, read for patterns.
public sealed partial class AiToolbox
{
    private async Task<object> AnalyzeBeatmapAsync(JsonElement args, CancellationToken cancellationToken)
    {
        int beatmapId = args.GetProperty("beatmap_id").GetInt32();
        if (await cachingHelper.GetOrCacheBeatmap(beatmapId, osuApi) is not { } beatmap)
            return new { error = "beatmap not found" };
        if (beatmap.CountCircles + beatmap.CountSliders + beatmap.CountSpinners >= 20_000)
            return new { error = "the beatmap is too long to analyze" };

        Playmode mode = ReadMode(args, "mode") ?? beatmap.Mode?.ParseRulesetToPlaymode() ?? Playmode.Osu;
        if (!TryReadMods(args, mode, out GameMod[] mods, out string modsText))
            return new { error = $"unknown mods {modsText}" };

        // The beatmap cache keeps every .osu file the bot has seen; only a missing or outdated one is downloaded.
        await using Stream file = await beatmapsService.DownloadOrCacheBeatmapAsync(beatmapId, beatmap.Checksum,
            cancellationToken);
        BeatmapPatterns patterns = BeatmapPatternAnalyzer.Analyze(file, mode, mods);

        // osu!'s own split of the difficulty: is the map hard to aim or hard to tap.
        file.Position = 0;
        PPCalculationResult? difficulty = await performanceCalculator.CalculatePpAsync(beatmapId, file, accuracy: 1,
            scoreMods: mods, rulesetId: (int)mode, cancellationToken: cancellationToken);
        var osuDifficulty = difficulty?.DifficultyAttributes as OfficialOsuDifficulty;

        BeatmapsetExtended? set = beatmap.BeatmapsetId is { } setId
            ? await cachingHelper.GetOrCacheBeatmapset(setId, osuApi)
            : null;
        return new
        {
            Beatmap = $"{set?.Artist} - {set?.Title} [{beatmap.Version}]",
            Mods = mods.Length == 0 ? "NM" : string.Concat(mods.Select(m => m.Acronym)),
            Stars = difficulty is null ? null : (double?)Math.Round(difficulty.DifficultyAttributes.StarRating, 2),
            AimStars = osuDifficulty is null ? null : (double?)Math.Round(osuDifficulty.AimDifficulty, 2),
            SpeedStars = osuDifficulty is null ? null : (double?)Math.Round(osuDifficulty.SpeedDifficulty, 2),
            Patterns = patterns,
            Link = $"https://osu.ppy.sh/b/{beatmapId}"
        };
    }

    /// <summary>The "mods" argument (like HDDT, +HR or NM) as osu! mods of the given mode.</summary>
    private static bool TryReadMods(JsonElement args, Playmode mode, out GameMod[] mods, out string modsText)
    {
        modsText = (args.GetProperty("mods").GetString() ?? "").Replace("+", "").Replace(" ", "").Replace(",", "");
        if (modsText.Equals("NM", StringComparison.OrdinalIgnoreCase))
            modsText = "";
        return modsText.TryParseMods(mode, out mods);
    }
}
