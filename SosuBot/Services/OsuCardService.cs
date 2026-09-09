using Microsoft.Extensions.Logging;
using OsuApi.BanchoV2;
using OsuApi.BanchoV2.Clients.Users.HttpIO;
using OsuApi.BanchoV2.Models;
using OsuApi.BanchoV2.Users.Models;
using SosuBot.Calculators.Official;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.Graphics;
using SosuBot.Graphics.Models;
using SosuBot.Helpers;
using SosuBot.PerformanceCalculator;
using System.Globalization;
using System.Text;
using OfficialOsuDifficultyAttributes = osu.Game.Rulesets.Osu.Difficulty.OsuDifficultyAttributes;

namespace SosuBot.Services;

public sealed class OsuCardService
{
    private const int MaximumParallelCalculations = 4;
    private const int MaximumAvatarBytes = 5 * 1024 * 1024;

    private readonly BanchoApiV2 _osuApi;
    private readonly BeatmapsService _beatmapsService;
    private readonly CachingHelper _cachingHelper;
    private readonly ProfileCardGenerator _cardGenerator;
    private readonly PlayerSkillCalculator _skillCalculator;
    private readonly OfficialPerformanceHelper _officialPerformanceHelper;
    private readonly HttpClient _httpClient;
    private readonly ILogger<OsuCardService> _logger;

    public OsuCardService(
        BanchoApiV2 osuApi,
        BeatmapsService beatmapsService,
        CachingHelper cachingHelper,
        ProfileCardGenerator cardGenerator,
        PlayerSkillCalculator skillCalculator,
        OfficialPerformanceHelper officialPerformanceHelper,
        IHttpClientFactory httpClientFactory,
        ILogger<OsuCardService> logger)
    {
        _osuApi = osuApi;
        _beatmapsService = beatmapsService;
        _cachingHelper = cachingHelper;
        _cardGenerator = cardGenerator;
        _skillCalculator = skillCalculator;
        _officialPerformanceHelper = officialPerformanceHelper;
        _httpClient = httpClientFactory.CreateClient("CustomHttpClient");
        _logger = logger;
    }

    public async Task<OsuCardGenerationResult> GenerateAsync(UserExtend user, Playmode playmode,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (user.Id is null || string.IsNullOrWhiteSpace(user.Username))
            return OsuCardGenerationResult.CalculationFailed();

        GetUserScoresResponse? response = await _osuApi.Users.GetUserScores(
            user.Id.Value,
            ScoreType.Best,
            new GetUserScoreQueryParameters
            {
                Limit = PlayerSkillCalculator.MaximumScoreCount,
                Mode = playmode.ToRuleset(),
                IncludeFails = 0
            },
            cancellationToken);

        Score[] scores = response?.Scores.Take(PlayerSkillCalculator.MaximumScoreCount).ToArray() ?? [];
        if (scores.Length == 0)
            return OsuCardGenerationResult.NoScores();

        using SemaphoreSlim semaphore = new(MaximumParallelCalculations);
        Task<PlayerScoreSkillInput?>[] calculations = scores
            .Select(score => CreateSkillInputWithLimitAsync(score, playmode, semaphore, cancellationToken))
            .ToArray();
        PlayerScoreSkillInput[] inputs = (await Task.WhenAll(calculations))
            .OfType<PlayerScoreSkillInput>()
            .ToArray();

        if (inputs.Length == 0)
            return OsuCardGenerationResult.CalculationFailed(scores.Length);

        PlayerSkills skills = _skillCalculator.Calculate(inputs);

        byte[]? avatar = await DownloadAvatarAsync(user.AvatarUrl, cancellationToken);
        using MemoryStream image = _cardGenerator.Generate(new ProfileCardData
        {
            Username = user.Username,
            PlayerId = user.Id.Value,
            Mode = (OsuGameMode)(int)playmode,
            Skills = skills,
            Avatar = avatar
        });

        return OsuCardGenerationResult.Success(image.ToArray(), skills, scores.Length);
    }

    private async Task<PlayerScoreSkillInput?> CreateSkillInputWithLimitAsync(Score score, Playmode playmode,
        SemaphoreSlim semaphore, CancellationToken cancellationToken)
    {
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            if (score.BeatmapId is null || score.Accuracy is null || score.MaxCombo is null)
                return null;

            try
            {
                // The normal path is completely independent of CachingHelper and osu! API
                // beatmap metadata: BeatmapsService reads the local .osu cache first, then
                // downloads and stores the file when it is missing.
                PlayerScoreSkillInput? cachedInput = await CreateCachedSkillInputAsync(score, playmode,
                    cancellationToken);
                if (cachedInput is not null)
                    return cachedInput;

                _logger.LogDebug(
                    "Official local calculation returned no result for beatmap {BeatmapId}; using API metadata fallback",
                    score.BeatmapId);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogDebug(exception,
                    "Could not load or calculate beatmap {BeatmapId} locally; using API metadata fallback",
                    score.BeatmapId);
            }

            return await CreateApiMetadataFallbackSkillInputAsync(score, playmode, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Could not calculate card skill for beatmap {BeatmapId}", score.BeatmapId);
            return null;
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task<PlayerScoreSkillInput?> CreateCachedSkillInputAsync(Score score, Playmode playmode,
        CancellationToken cancellationToken)
    {
        if (score.BeatmapId is null || score.Accuracy is null || score.MaxCombo is null)
            return null;

        using Stream beatmap = await _beatmapsService.DownloadOrCacheBeatmapAsync(score.BeatmapId.Value,
            cancellationToken);
        BeatmapFileValues fileValues = ReadBeatmapValues(beatmap);
        OfficialScoreCalculation scoreCalculation = await _officialPerformanceHelper.CalculateScoreAsync(
            beatmap,
            score,
            playmode,
            calculateCurrent: true,
            cancellationToken: cancellationToken);
        PPCalculationResult? calculation = scoreCalculation.Current;

        if (calculation is null)
            return null;

        var difficulty = calculation.DifficultyAttributes;
        int playableObjectCount = Math.Max(calculation.BeatmapHitObjectsCount, fileValues.TotalObjectCount);
        int sliderCount = GetSliderCount(playmode, difficulty, fileValues, playableObjectCount);
        int hitCircleCount = Math.Max(playableObjectCount - sliderCount, 0);
        double speedChangeFactor = calculation.SpeedChangeFactor > 0 &&
                                   double.IsFinite(calculation.SpeedChangeFactor)
            ? calculation.SpeedChangeFactor
            : 1;

        double aimDifficulty = 0;
        double speedDifficulty = 0;
        double speedNoteCount = GetDensityNoteCount(playmode, difficulty, fileValues, playableObjectCount);
        if (difficulty is OfficialOsuDifficultyAttributes osuDifficulty)
        {
            aimDifficulty = osuDifficulty.AimDifficulty;
            speedDifficulty = osuDifficulty.SpeedDifficulty;
            speedNoteCount = osuDifficulty.SpeedNoteCount;
            hitCircleCount = Math.Max(osuDifficulty.HitCircleCount, 0);
            sliderCount = Math.Max(osuDifficulty.SliderCount, 0);
        }

        return new PlayerScoreSkillInput
        {
            Mode = (OsuGameMode)(int)playmode,
            StarRating = difficulty.StarRating,
            AccuracyPercent = score.Accuracy.Value * 100,
            Bpm = fileValues.AverageBpm * speedChangeFactor,
            CircleSize = calculation.CS,
            ApproachRate = calculation.AR,
            OverallDifficulty = calculation.OD,
            DrainRate = calculation.HP,
            Combo = score.MaxCombo.Value,
            MaximumCombo = calculation.BeatmapMaxCombo,
            HitCircleCount = hitCircleCount,
            SliderCount = sliderCount,
            HitLength = (int)(fileValues.HitLength / speedChangeFactor),
            // These fields are mode-neutral legacy DTO slots. Only osu!standard
            // exposes genuine Aim/Speed values; other rulesets use their own
            // DifficultyAttributes and a file-derived note count for density.
            AimDifficulty = aimDifficulty,
            SpeedDifficulty = speedDifficulty,
            SpeedNoteCount = speedNoteCount,
            Mods = GetModAcronyms(score)
        };
    }

    private async Task<PlayerScoreSkillInput?> CreateApiMetadataFallbackSkillInputAsync(Score score, Playmode playmode,
        CancellationToken cancellationToken)
    {
        if (score.BeatmapId is null || score.Accuracy is null || score.MaxCombo is null)
            return null;

        // This is deliberately a last-resort fallback. A cache hit here is local,
        // while a cache miss causes CachingHelper to request beatmap metadata from osu! API.
        BeatmapExtended? beatmap = await _cachingHelper.GetOrCacheBeatmap(score.BeatmapId.Value, _osuApi,
            cancellationToken);
        if (beatmap is null)
            return null;

        string[] mods = GetModAcronyms(score);
        LegacyBeatmapValues values = ApplyLegacyMods(beatmap, score.Beatmap?.DifficultyRating, playmode, mods);
        return new PlayerScoreSkillInput
        {
            Mode = (OsuGameMode)(int)playmode,
            StarRating = values.StarRating,
            AccuracyPercent = score.Accuracy.Value * 100,
            Bpm = values.Bpm,
            CircleSize = values.CircleSize,
            ApproachRate = values.ApproachRate,
            OverallDifficulty = values.OverallDifficulty,
            DrainRate = values.DrainRate,
            Combo = score.MaxCombo.Value,
            MaximumCombo = beatmap.MaxCombo ?? Math.Max(score.MaxCombo.Value, 1),
            HitCircleCount = beatmap.CountCircles ?? 0,
            SliderCount = beatmap.CountSliders ?? 0,
            HitLength = beatmap.HitLength ?? 0,
            // ПРИБЛИЗИТЕЛЬНО: нет данных о паттерне/синкопации в DTO; taiko
            // per-mode values are unavailable, so no genuine Rhythm metric is calculated.
            AimDifficulty = 0,
            SpeedDifficulty = 0,
            SpeedNoteCount = 0,
            Mods = mods
        };
    }

    private static LegacyBeatmapValues ApplyLegacyMods(BeatmapExtended beatmap, double? scoreStarRating,
        Playmode playmode, string[] mods)
    {
        double stars = scoreStarRating ?? beatmap.DifficultyRating ?? 0;
        double bpm = beatmap.BPM ?? 0;
        double circleSize = beatmap.CS ?? 0;
        double approachRate = beatmap.AR ?? 0;
        double overallDifficulty = beatmap.Accuracy ?? 0;
        double drainRate = beatmap.Drain ?? 0;

        bool easy = HasAnyMod(mods, "EZ");
        bool hardRock = HasAnyMod(mods, "HR");
        bool doubleTime = HasAnyMod(mods, "DT", "NC");
        bool halfTime = HasAnyMod(mods, "HT", "DC");

        if (easy)
        {
            if (playmode != Playmode.Mania)
                circleSize /= 2;
            approachRate /= 2;
            overallDifficulty /= 2;
            drainRate /= 2;
            if (playmode == Playmode.Catch)
                stars /= 1.25;
        }

        if (hardRock)
        {
            if (playmode != Playmode.Mania)
                circleSize *= 1.3;
            approachRate = Math.Min(approachRate * 1.4, 10);
            overallDifficulty = Math.Min(overallDifficulty * 1.4, 10);
            drainRate = Math.Min(drainRate * 1.4, 10);
            if (playmode == Playmode.Catch)
                stars *= 1.13;
        }

        if (doubleTime)
        {
            bpm *= 1.5;
            ApplyDoubleTimeDifficulty(playmode, ref approachRate, ref overallDifficulty);
            stars *= playmode switch
            {
                Playmode.Taiko => 1.3,
                Playmode.Catch or Playmode.Mania => 1.4,
                _ => 1
            };
        }

        if (halfTime)
        {
            bpm /= 1.33;
            ApplyHalfTimeDifficulty(playmode, ref approachRate, ref overallDifficulty);
            stars /= playmode switch
            {
                Playmode.Taiko => 1.2,
                Playmode.Catch or Playmode.Mania => 1.25,
                _ => 1
            };
        }

        return new LegacyBeatmapValues(
            StarRating: Math.Max(stars, 0),
            Bpm: Math.Max(bpm, 0),
            CircleSize: Math.Clamp(circleSize, 0, 11),
            ApproachRate: Math.Clamp(approachRate, 0, 11),
            OverallDifficulty: Math.Clamp(overallDifficulty, 0, 11),
            DrainRate: Math.Clamp(drainRate, 0, 11));
    }

    private static void ApplyDoubleTimeDifficulty(Playmode playmode, ref double approachRate,
        ref double overallDifficulty)
    {
        if (playmode is Playmode.Osu or Playmode.Catch)
        {
            double arMilliseconds = approachRate < 6
                ? 800 + (5 - approachRate) * 80
                : 800 - (approachRate - 5) * 100;
            approachRate = arMilliseconds <= 1200
                ? 5 + (1200 - arMilliseconds) / 150
                : 5 - (1200 - arMilliseconds) / 120;

            double odMilliseconds = 53 - overallDifficulty * 4;
            overallDifficulty = (79.5 - odMilliseconds) / 6;
        }
        else if (playmode == Playmode.Taiko)
        {
            double odMilliseconds = 33.33 - overallDifficulty * 2;
            overallDifficulty = (49.5 - odMilliseconds) / 3;
        }
    }

    private static void ApplyHalfTimeDifficulty(Playmode playmode, ref double approachRate,
        ref double overallDifficulty)
    {
        if (playmode is Playmode.Osu or Playmode.Catch)
        {
            double arMilliseconds = approachRate < 6
                ? 1600 + (5 - approachRate) * 160
                : 1600 - (approachRate - 5) * 200;
            approachRate = arMilliseconds <= 1200
                ? 5 + (1200 - arMilliseconds) / 150
                : 5 - (1200 - arMilliseconds) / 120;

            double odMilliseconds = 106 - overallDifficulty * 8;
            overallDifficulty = (79.5 - odMilliseconds) / 6;
        }
        else if (playmode == Playmode.Taiko)
        {
            double odMilliseconds = 66.66 - overallDifficulty * 4;
            overallDifficulty = (49.5 - odMilliseconds) / 3;
        }
    }

    private async Task<byte[]?> DownloadAvatarAsync(string? avatarUrl, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(avatarUrl))
            return null;

        if (!Uri.TryCreate(avatarUrl, UriKind.Absolute, out Uri? uri))
            uri = new Uri(new Uri("https://osu.ppy.sh"), avatarUrl);

        try
        {
            using HttpResponseMessage response = await _httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > MaximumAvatarBytes)
                return null;

            byte[] content = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            return content.Length <= MaximumAvatarBytes ? content : null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Could not download osu! avatar from {AvatarUrl}", uri);
            return null;
        }
    }

    private static string[] GetModAcronyms(Score score) => (score.Mods ?? [])
        .Select(mod => mod.Acronym?.ToUpperInvariant())
        .OfType<string>()
        .ToArray();

    private static bool HasAnyMod(string[] mods, params string[] acronyms) =>
        mods.Any(mod => acronyms.Contains(mod, StringComparer.OrdinalIgnoreCase));

    private static int GetSliderCount(Playmode playmode,
        osu.Game.Rulesets.Difficulty.DifficultyAttributes difficulty, BeatmapFileValues fileValues,
        int playableObjectCount)
    {
        int sliderCount = playmode switch
        {
            Playmode.Osu when difficulty is OfficialOsuDifficultyAttributes osuDifficulty => osuDifficulty.SliderCount,
            Playmode.Mania => fileValues.LongNoteCount,
            _ => fileValues.SliderCount
        };

        return Math.Clamp(sliderCount, 0, Math.Max(playableObjectCount, 0));
    }

    private static double GetDensityNoteCount(Playmode playmode,
        osu.Game.Rulesets.Difficulty.DifficultyAttributes difficulty, BeatmapFileValues fileValues,
        int playableObjectCount)
    {
        if (playmode == Playmode.Osu && difficulty is OfficialOsuDifficultyAttributes osuDifficulty)
            return Math.Max(osuDifficulty.SpeedNoteCount, 0);

        // Taiko, catch and mania do not expose osu!standard's SpeedNoteCount.
        // Their PlayerScoreSkillInput currently has no dedicated mode-specific
        // density field, so use the playable note count as the neutral fallback.
        return Math.Max(playableObjectCount > 0 ? playableObjectCount : fileValues.TotalObjectCount, 0);
    }

    private static BeatmapFileValues ReadBeatmapValues(Stream beatmap)
    {
        if (!beatmap.CanSeek)
            throw new InvalidOperationException("The beatmap stream must be seekable to read metadata.");

        beatmap.Position = 0;
        using StreamReader reader = new(beatmap, Encoding.UTF8, detectEncodingFromByteOrderMarks: true,
            bufferSize: 4096, leaveOpen: true);
        bool timingPointsSection = false;
        bool hitObjectsSection = false;
        double bpmTotal = 0;
        int timingPointCount = 0;
        int sliderCount = 0;
        int longNoteCount = 0;
        int totalObjectCount = 0;
        double firstObjectTime = double.PositiveInfinity;
        double lastObjectEndTime = double.NegativeInfinity;

        while (reader.ReadLine() is { } line)
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith('['))
            {
                timingPointsSection = trimmed.Equals("[TimingPoints]", StringComparison.Ordinal);
                hitObjectsSection = trimmed.Equals("[HitObjects]", StringComparison.Ordinal);
                continue;
            }

            if (trimmed.Length == 0 || trimmed.StartsWith("//", StringComparison.Ordinal))
                continue;

            if (timingPointsSection)
            {
                string[] timingValues = trimmed.Split(',');
                if (timingValues.Length >= 2
                    && double.TryParse(timingValues[1], NumberStyles.Float, CultureInfo.InvariantCulture,
                        out double millisecondsPerBeat)
                    && millisecondsPerBeat > 0)
                {
                    bpmTotal += 60_000 / millisecondsPerBeat;
                    timingPointCount++;
                }
            }

            if (!hitObjectsSection)
                continue;

            string[] values = trimmed.Split(',');
            if (values.Length < 4
                || !double.TryParse(values[2], NumberStyles.Float, CultureInfo.InvariantCulture,
                    out double startTime)
                || !int.TryParse(values[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int type)
                || (type & (1 | 2 | 8 | 128)) == 0)
                continue;

            totalObjectCount++;
            if ((type & 2) != 0)
                sliderCount++;
            if ((type & 128) != 0)
                longNoteCount++;

            double endTime = startTime;
            if ((type & 8) != 0 && values.Length > 5)
            {
                // Spinner end time.
                double.TryParse(values[5], NumberStyles.Float, CultureInfo.InvariantCulture, out endTime);
            }
            else if ((type & 128) != 0 && values.Length > 5)
            {
                // Mania hold end time is the first value before the ':' separator.
                string holdEndTime = values[5].Split(':', 2)[0];
                double.TryParse(holdEndTime, NumberStyles.Float, CultureInfo.InvariantCulture, out endTime);
            }

            firstObjectTime = Math.Min(firstObjectTime, startTime);
            lastObjectEndTime = Math.Max(lastObjectEndTime, endTime);
        }

        beatmap.Position = 0;
        double averageBpm = timingPointCount == 0
            ? 0
            : Math.Floor(bpmTotal / timingPointCount + 0.5);
        int hitLength = !double.IsFinite(firstObjectTime) || !double.IsFinite(lastObjectEndTime)
            ? 0
            : Math.Max(0, (int)Math.Ceiling((lastObjectEndTime - firstObjectTime) / 1000));

        return new BeatmapFileValues(
            AverageBpm: averageBpm,
            HitLength: hitLength,
            SliderCount: sliderCount,
            LongNoteCount: longNoteCount,
            TotalObjectCount: totalObjectCount);
    }

    private sealed record LegacyBeatmapValues(
        double StarRating,
        double Bpm,
        double CircleSize,
        double ApproachRate,
        double OverallDifficulty,
        double DrainRate);

    private sealed record BeatmapFileValues(
        double AverageBpm,
        int HitLength,
        int SliderCount,
        int LongNoteCount,
        int TotalObjectCount);
}

public sealed record OsuCardGenerationResult(
    byte[]? Image,
    PlayerSkills? Skills,
    int RequestedScores,
    OsuCardGenerationFailure Failure)
{
    public static OsuCardGenerationResult Success(byte[] image, PlayerSkills skills, int requestedScores) =>
        new(image, skills, requestedScores, OsuCardGenerationFailure.None);

    public static OsuCardGenerationResult NoScores() =>
        new(null, null, 0, OsuCardGenerationFailure.NoScores);

    public static OsuCardGenerationResult CalculationFailed(int requestedScores = 0) =>
        new(null, null, requestedScores, OsuCardGenerationFailure.CalculationFailed);
}

public enum OsuCardGenerationFailure
{
    None,
    NoScores,
    CalculationFailed
}
