using Microsoft.Extensions.Logging;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Catch;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.Taiko;
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;
using OsuApi.BanchoV2;
using OsuApi.BanchoV2.Clients.Beatmaps.HttpIO;
using OsuApi.BanchoV2.Models;
using OsuApi.BanchoV2.Users.Models;
using OsuParsers.Replays;
using SosuBot.Calculators.Official;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.Graphics;
using SosuBot.Graphics.Models;
using SosuBot.Helpers;
using SosuBot.Localization;
using SosuBot.PerformanceCalculator;
using Telegram.Bot;
using Telegram.Bot.Types;
using GameMod = osu.Game.Rulesets.Mods.Mod;
using LegacyMods = OsuParsers.Enums.Mods;
using OnlineScore = OsuApi.BanchoV2.Models.Score;

namespace SosuBot.Services;

/// <summary>Everything osu! itself uses to work out the pp of a play, unlike the legacy counts of a replay.</summary>
public sealed record ReplayFullScore(
    Dictionary<HitResult, int> Statistics,
    Dictionary<HitResult, int> MaximumStatistics,
    GameMod[] Mods,
    long? LegacyTotalScore);

/// <summary>
/// Answers a replay file sent to a chat with a card of the play: the player, a link to the beatmap, a picture with
/// the map and the score (<see cref="ReplayCardGenerator" />), and a hint that /render turns it into a video.
/// </summary>
public sealed class ReplayCardService(
    BanchoApiV2 osuApi,
    CachingHelper cachingHelper,
    BeatmapsService beatmapsService,
    IPerformanceCalculator performanceCalculator,
    OsuProfileImageService imageService,
    ReplayCardGenerator generator,
    ILogger<ReplayCardService> logger)
{
    /// <summary>Real replays are well under a megabyte; anything bigger is not worth downloading.</summary>
    private const long MaximumReplayBytes = 10 * 1024 * 1024;

    /// <summary>Replays written by osu!lazer have versions from 30000000 on; older ones come from stable.</summary>
    private const int FirstLazerReplayVersion = 30_000_000;

    // Legacy mod bits in the order players write them; NC and PF replace DT and SD.
    private static readonly (LegacyMods Mod, string Acronym)[] ModAcronyms =
    [
        (LegacyMods.Easy, "EZ"), (LegacyMods.NoFail, "NF"), (LegacyMods.HalfTime, "HT"),
        (LegacyMods.Hidden, "HD"), (LegacyMods.FadeIn, "FI"), (LegacyMods.HardRock, "HR"),
        (LegacyMods.Nightcore, "NC"), (LegacyMods.DoubleTime, "DT"), (LegacyMods.Flashlight, "FL"),
        (LegacyMods.Perfect, "PF"), (LegacyMods.SuddenDeath, "SD"), (LegacyMods.SpunOut, "SO"),
        (LegacyMods.Relax, "RX"), (LegacyMods.Relax2, "AP"), (LegacyMods.TouchDevice, "TD"),
        (LegacyMods.Random, "RD"), (LegacyMods.Key1, "1K"), (LegacyMods.Key2, "2K"), (LegacyMods.Key3, "3K"),
        (LegacyMods.Key4, "4K"), (LegacyMods.Key5, "5K"), (LegacyMods.Key6, "6K"), (LegacyMods.Key7, "7K"),
        (LegacyMods.Key8, "8K"), (LegacyMods.Key9, "9K"), (LegacyMods.ScoreV2, "SV2")
    ];

    public static bool IsReplayFile(Document? document) =>
        document?.FileName?.EndsWith(".osr", StringComparison.OrdinalIgnoreCase) == true;

    public async Task SendAsync(ITelegramBotClient bot, Message message, ILocalization language,
        CancellationToken cancellationToken)
    {
        Document document = message.Document!;
        if (document.FileSize > MaximumReplayBytes)
            return;

        Replay replay;
        ReplayFileScore fileScore;
        try
        {
            byte[] file = await DownloadAsync(bot, document, cancellationToken);
            using var stream = new MemoryStream(ReplayFileCompatibility.NormalizeLifeBar(file), writable: false);
            replay = OsuParsers.Decoders.ReplayDecoder.Decode(stream);
            fileScore = ReplayFileCompatibility.ReadScore(file);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogInformation(exception, "A sent .osr file could not be read");
            await message.ReplyAsync(bot, language.replayCard_invalid);
            return;
        }

        LookupBeatmapResponse? lookup = await osuApi.Beatmaps.LookupBeatmap(new LookupBeatmapQueryParameters
            { Checksum = replay.BeatmapMD5Hash });
        if (lookup?.BeatmapExtended is not { Id: { } beatmapId } beatmap)
        {
            await message.ReplyAsync(bot, language.replayCard_beatmapNotFound.Fill([Escape(replay.PlayerName)]));
            return;
        }

        BeatmapsetExtended? beatmapset = beatmap.BeatmapsetId is { } setId
            ? await cachingHelper.GetOrCacheBeatmapset(setId, osuApi)
            : null;
        string title = $"{beatmapset?.Artist} - {beatmapset?.Title} [{beatmap.Version}]";
        string caption = language.replayCard_caption.Fill([
            $"https://osu.ppy.sh/users/{Uri.EscapeDataString(replay.PlayerName ?? "")}",
            Escape(replay.PlayerName),
            $"https://osu.ppy.sh/b/{beatmapId}",
            Escape(title)
        ]);

        try
        {
            ReplayCardData card = await BuildCardAsync(replay, fileScore, beatmap, beatmapset, cancellationToken);
            await using MemoryStream picture = generator.Generate(card);
            await message.ReplyPhotoAsync(bot, InputFile.FromStream(picture, "replay.png"), caption);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The card is a nicety: without it the player still gets the beatmap and the /render hint.
            logger.LogWarning(exception, "Could not draw the replay card for beatmap {BeatmapId}", beatmapId);
            await message.ReplyAsync(bot, caption);
        }
    }

    private async Task<ReplayCardData> BuildCardAsync(Replay replay, ReplayFileScore fileScore, BeatmapExtended beatmap,
        BeatmapsetExtended? beatmapset, CancellationToken cancellationToken)
    {
        var mode = (Playmode)Math.Clamp((int)replay.Ruleset, 0, 3);
        string[] acronyms = Acronyms(replay.Mods);
        // Stable plays are scored as Classic, the same way osu! counts them on the website.
        string modsForCalculation = string.Concat(acronyms.Where(a => a is not ("TD" or "SV2" or "RD"))) +
                                    (replay.OsuVersion < FirstLazerReplayVersion ? "CL" : "");
        modsForCalculation.TryParseMods(mode, out GameMod[] legacyMods);

        ReplayFullScore? fullScore = await FindFullScoreAsync(fileScore, replay, beatmap.Id!.Value, mode, cancellationToken);
        GameMod[] mods = fullScore?.Mods ?? legacyMods;

        long maxScore = MaxStandardisedScore(mode, new BeatmapDifficulty
        {
            CircleSize = beatmap.CS ?? 5,
            ApproachRate = beatmap.AR ?? 5,
            OverallDifficulty = beatmap.Accuracy ?? 5,
            DrainRate = beatmap.Drain ?? 5
        }, mods);

        Task<byte[]?> cover = beatmapset?.Id is { } setId
            ? imageService.DownloadImageAsync($"https://assets.ppy.sh/beatmaps/{setId}/covers/cover@2x.jpg",
                cancellationToken)
            : Task.FromResult<byte[]?>(null);

        Dictionary<HitResult, int> statistics = Statistics(replay, mode);
        PPCalculationResult? play = null, fullCombo = null, perfect = null;
        try
        {
            await using Stream file = await beatmapsService.DownloadOrCacheBeatmapAsync(beatmap.Id!.Value,
                beatmap.Checksum, cancellationToken);
            play = await Calculate(file, accuracy: null, replay.Combo, fullScore?.Statistics ?? statistics, fullScore);
            double accuracy = play?.CalculatedAccuracy ?? 1;
            fullCombo = await Calculate(file, accuracy, combo: null, statistics: null, fullScore: null);
            perfect = await Calculate(file, accuracy: 1, combo: null, statistics: null, fullScore: null);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Could not calculate pp for a replay on beatmap {BeatmapId}", beatmap.Id);
        }

        double rate = OfficialPerformanceHelper.GetClockRate(mods);
        bool hardRock = acronyms.Contains("HR"), easy = acronyms.Contains("EZ");
        double Scale(double value, double hardRockFactor) =>
            Math.Min(10, hardRock ? value * hardRockFactor : easy ? value / 2 : value);

        double approachRate = Scale(beatmap.AR ?? 0, 1.4);
        double overallDifficulty = Scale(beatmap.Accuracy ?? 0, 1.4);
        if (mode == Playmode.Osu && rate != 1)
        {
            approachRate = RateAdjustedApproachRate(approachRate, rate);
            overallDifficulty = (80 - (80 - 6 * overallDifficulty) / rate) / 6;
        }

        return new ReplayCardData
        {
            Title = beatmapset?.Title ?? "",
            Artist = beatmapset?.Artist ?? "",
            DifficultyName = beatmap.Version ?? "",
            Mapper = beatmapset?.Creator ?? "",
            Status = beatmap.Status ?? "",
            Mode = (OsuGameMode)mode,
            Background = await cover,
            LengthSeconds = (int)Math.Round((beatmap.TotalLength ?? 0) / rate),
            MaxCombo = MaximumCombo(mode, replay.OsuVersion < FirstLazerReplayVersion, beatmap.ModeInt,
                play?.BeatmapMaxCombo, beatmap.MaxCombo),
            Bpm = (beatmap.BPM ?? 0) * rate,
            CircleSize = mode == Playmode.Mania ? beatmap.CS ?? 0 : Scale(beatmap.CS ?? 0, 1.3),
            ApproachRate = approachRate,
            Drain = Scale(beatmap.Drain ?? 0, 1.4),
            OverallDifficulty = overallDifficulty,
            StarRating = play?.DifficultyAttributes.StarRating ?? beatmap.DifficultyRating ?? 0,
            Mods = acronyms,
            Score = replay.ReplayScore,
            MaxScore = maxScore,
            AccuracyPercent = (play?.CalculatedAccuracy ?? LegacyAccuracy(replay, mode)) * 100,
            Combo = replay.Combo,
            Pp = play?.PP,
            FullComboPp = fullCombo?.PP,
            PerfectPp = perfect?.PP,
            Hits = Hits(replay, mode)
        };

        async Task<PPCalculationResult?> Calculate(Stream file, double? accuracy, int? combo,
            Dictionary<HitResult, int>? statistics, ReplayFullScore? fullScore)
        {
            file.Position = 0;
            return await performanceCalculator.CalculatePpAsync(beatmap.Id!.Value, file, accuracy,
                scoreMaxCombo: combo, scoreMods: mods, scoreStatistics: statistics, rulesetId: (int)mode,
                cancellationToken: cancellationToken, maximumStatistics: fullScore?.MaximumStatistics,
                legacyTotalScore: fullScore?.LegacyTotalScore);
        }
    }

    /// <summary>
    /// The combo a play could reach. In osu!mania a long note gives exactly 2 combo in osu!lazer, which is what the
    /// calculator counts, and more in osu!stable, which is what the osu! API reports for the beatmap. The API's number
    /// describes the beatmap in its own mode, so it says nothing about a mania conversion of another mode's beatmap.
    /// </summary>
    public static int MaximumCombo(Playmode mode, bool stableReplay, int? beatmapMode, int? calculatedCombo, int? apiCombo)
    {
        bool stableMania = mode == Playmode.Mania && stableReplay && beatmapMode == (int)Playmode.Mania;
        return (stableMania ? apiCombo ?? calculatedCombo : calculatedCombo ?? apiCombo) ?? 0;
    }

    /// <summary>
    /// The legacy counts of a replay leave out slider ends and ticks, so pp from them alone drifts from the website.
    /// osu!lazer keeps the full result in the file; for other replays the osu! servers know it by the score's id.
    /// </summary>
    private async Task<ReplayFullScore?> FindFullScoreAsync(ReplayFileScore fileScore, Replay replay, int beatmapId,
        Playmode mode, CancellationToken cancellationToken)
    {
        try
        {
            if (FullScoreFromLazerReplay(fileScore.LazerScore, mode) is { } lazerScore)
                return lazerScore;
            if (fileScore.OnlineId is not > 0)
                return null;

            OnlineScore? onlineScore = await osuApi.MakeRequestAsync<OnlineScore>(
                $"{BanchoApiV2.ApiMainFunctionsBaseAddress.TrimEnd('/')}/scores/{mode.ToRuleset()}/{fileScore.OnlineId}",
                HttpMethod.Get, cancellationToken: cancellationToken);
            return onlineScore is null ? null : FullScoreFromOnlineScore(onlineScore, beatmapId, replay.Combo, mode);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Without the full result the card still works, with pp estimated from the legacy counts.
            logger.LogInformation(exception, "Could not get the full result of a replay on beatmap {BeatmapId}", beatmapId);
            return null;
        }
    }

    /// <summary>The result stored in an osu!lazer replay. Failed plays have no pp on the website and stay an estimate.</summary>
    public static ReplayFullScore? FullScoreFromLazerReplay(LegacyReplaySoloScoreInfo? score, Playmode mode)
    {
        if (score is null || score.Rank == ScoreRank.F || score.Statistics.Count == 0 ||
            score.MaximumStatistics.Count == 0)
            return null;

        osu.Game.Rulesets.Ruleset ruleset = CreateRuleset(mode);
        GameMod[] mods = score.Mods.Select(mod => mod.ToMod(ruleset)).ToArray();
        return mods.Any(mod => mod is UnknownMod)
            ? null
            : new ReplayFullScore(score.Statistics, score.MaximumStatistics, mods, null);
    }

    /// <summary>
    /// The result the osu! servers keep for a replay's id. A replay can claim any id, so the score counts only when it
    /// was set on the same beatmap with the same combo.
    /// </summary>
    public static ReplayFullScore? FullScoreFromOnlineScore(OnlineScore score, int beatmapId, int combo, Playmode mode)
    {
        if (score.Passed == false || score.Statistics is null || score.MaximumStatistics is null ||
            score.BeatmapId != beatmapId || score.MaxCombo != combo)
            return null;

        return new ReplayFullScore(score.Statistics.ToStatistics(), score.MaximumStatistics.ToStatistics(),
            (score.Mods ?? []).ToOsuMods(mode),
            score.LegacyTotalScore is { } legacyScore and <= long.MaxValue ? (long)legacyScore : null);
    }

    /// <summary>
    /// 1,000,000 times the score multiplier of the mods, the most a standardised score can reach with them. osu!lazer
    /// works the multiplier out per ruleset and beatmap (<see cref="osu.Game.Rulesets.Ruleset.CreateScoreMultiplierCalculator" />),
    /// so <see cref="GameMod.ScoreMultiplier" /> alone is always 1. Classic is left out: it only changes how a score
    /// is shown, not its standardised value.
    /// </summary>
    public static long MaxStandardisedScore(Playmode mode, IBeatmapDifficultyInfo difficulty, IEnumerable<GameMod> mods)
    {
        GameMod[] scoredMods = mods.Where(mod => mod.Acronym != "CL").ToArray();
        var context = new ScoreMultiplierContext(difficulty, new ScoreInfo { Mods = scoredMods });
        return (long)Math.Round(1_000_000 *
                                CreateRuleset(mode).CreateScoreMultiplierCalculator(context).CalculateFor(scoredMods));
    }

    private static osu.Game.Rulesets.Ruleset CreateRuleset(Playmode mode) => mode switch
    {
        Playmode.Taiko => new TaikoRuleset(),
        Playmode.Catch => new CatchRuleset(),
        Playmode.Mania => new ManiaRuleset(),
        _ => new OsuRuleset()
    };

    public static string[] Acronyms(LegacyMods mods)
    {
        List<string> acronyms = ModAcronyms.Where(pair => mods.HasFlag(pair.Mod)).Select(pair => pair.Acronym).ToList();
        if (acronyms.Contains("NC"))
            acronyms.Remove("DT");
        if (acronyms.Contains("PF"))
            acronyms.Remove("SD");
        return acronyms.ToArray();
    }

    /// <summary>The replay's legacy counts as the hit results each ruleset scores.</summary>
    public static Dictionary<HitResult, int> Statistics(Replay replay, Playmode mode) => mode switch
    {
        Playmode.Taiko => new Dictionary<HitResult, int>
        {
            [HitResult.Great] = replay.Count300,
            [HitResult.Ok] = replay.Count100,
            [HitResult.Miss] = replay.CountMiss
        },
        Playmode.Catch => new Dictionary<HitResult, int>
        {
            [HitResult.Great] = replay.Count300,
            [HitResult.LargeTickHit] = replay.Count100,
            [HitResult.SmallTickHit] = replay.Count50,
            [HitResult.SmallTickMiss] = replay.CountKatu,
            [HitResult.Miss] = replay.CountMiss
        },
        Playmode.Mania => new Dictionary<HitResult, int>
        {
            [HitResult.Perfect] = replay.CountGeki,
            [HitResult.Great] = replay.Count300,
            [HitResult.Good] = replay.CountKatu,
            [HitResult.Ok] = replay.Count100,
            [HitResult.Meh] = replay.Count50,
            [HitResult.Miss] = replay.CountMiss
        },
        _ => new Dictionary<HitResult, int>
        {
            [HitResult.Great] = replay.Count300,
            [HitResult.Ok] = replay.Count100,
            [HitResult.Meh] = replay.Count50,
            [HitResult.Miss] = replay.CountMiss
        }
    };

    private static IReadOnlyList<(string Label, int Count)> Hits(Replay replay, Playmode mode) =>
        mode switch
    {
        Playmode.Taiko => [("300", replay.Count300), ("100", replay.Count100), ("Miss", replay.CountMiss)],
        Playmode.Mania =>
        [
            ("320", replay.CountGeki), ("300", replay.Count300), ("200", replay.CountKatu),
            ("100", replay.Count100), ("50", replay.Count50), ("Miss", replay.CountMiss)
        ],
        _ => [("300", replay.Count300), ("100", replay.Count100), ("50", replay.Count50), ("Miss", replay.CountMiss)]
    };

    /// <summary>Accuracy from the legacy counts, for when pp could not be calculated.</summary>
    private static double LegacyAccuracy(Replay replay, Playmode mode)
    {
        double n300 = replay.Count300, n100 = replay.Count100, n50 = replay.Count50, misses = replay.CountMiss;
        double geki = replay.CountGeki, katu = replay.CountKatu;
        (double hit, double total) = mode switch
        {
            Playmode.Taiko => (n300 + n100 / 2, n300 + n100 + misses),
            Playmode.Catch => (n300 + n100 + n50, n300 + n100 + n50 + katu + misses),
            Playmode.Mania => ((geki + n300) * 300 + katu * 200 + n100 * 100 + n50 * 50,
                (geki + n300 + katu + n100 + n50 + misses) * 300),
            _ => (n300 * 300 + n100 * 100 + n50 * 50, (n300 + n100 + n50 + misses) * 300)
        };
        return total > 0 ? hit / total : 0;
    }

    private static double RateAdjustedApproachRate(double approachRate, double rate)
    {
        double preempt = approachRate < 5 ? 1800 - 120 * approachRate : 1200 - 150 * (approachRate - 5);
        preempt /= rate;
        return preempt > 1200 ? (1800 - preempt) / 120 : 5 + (1200 - preempt) / 150;
    }


    private static async Task<byte[]> DownloadAsync(ITelegramBotClient bot, Document document,
        CancellationToken cancellationToken)
    {
        TGFile file = await bot.GetFile(document.FileId, cancellationToken);
        using var stream = new MemoryStream();
        await bot.DownloadFileConsideringLocalServer(file, stream);
        return stream.ToArray();
    }

    private static string Escape(string? text) => (text ?? "").EncodeHtml() ?? "";
}
