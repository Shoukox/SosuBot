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
using OfficialOsuDifficultyAttributes = osu.Game.Rulesets.Osu.Difficulty.OsuDifficultyAttributes;
using OfficialOsuPerformanceAttributes = osu.Game.Rulesets.Osu.Difficulty.OsuPerformanceAttributes;

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
    private readonly OsuStandardSkillCalculator _osuStandardSkillCalculator;
    private readonly OfficialPerformanceHelper _officialPerformanceHelper;
    private readonly HttpClient _httpClient;
    private readonly ILogger<OsuCardService> _logger;

    public OsuCardService(
        BanchoApiV2 osuApi,
        BeatmapsService beatmapsService,
        CachingHelper cachingHelper,
        ProfileCardGenerator cardGenerator,
        PlayerSkillCalculator skillCalculator,
        OsuStandardSkillCalculator osuStandardSkillCalculator,
        OfficialPerformanceHelper officialPerformanceHelper,
        IHttpClientFactory httpClientFactory,
        ILogger<OsuCardService> logger)
    {
        _osuApi = osuApi;
        _beatmapsService = beatmapsService;
        _cachingHelper = cachingHelper;
        _cardGenerator = cardGenerator;
        _skillCalculator = skillCalculator;
        _osuStandardSkillCalculator = osuStandardSkillCalculator;
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
                Limit = OsuStandardSkillCalculator.MaximumScoreCount,
                Mode = playmode.ToRuleset(),
                IncludeFails = 0
            },
            cancellationToken);

        Score[] scores = response?.Scores.Take(OsuStandardSkillCalculator.MaximumScoreCount).ToArray() ?? [];
        if (scores.Length == 0)
            return OsuCardGenerationResult.NoScores();

        PlayerSkills skills;
        OsuSkillRating? osuSkillRating = null;

        if (playmode == Playmode.Osu)
        {
            using SemaphoreSlim semaphore = new(MaximumParallelCalculations);
            Task<OsuStandardScoreSkillInput?>[] calculations = scores
                .Select(score => CreateOsuSkillInputWithLimitAsync(score, semaphore, cancellationToken))
                .ToArray();
            OsuStandardScoreSkillInput[] inputs = (await Task.WhenAll(calculations))
                .OfType<OsuStandardScoreSkillInput>()
                .ToArray();

            if (inputs.Length == 0)
                return OsuCardGenerationResult.CalculationFailed(scores.Length);

            osuSkillRating = _osuStandardSkillCalculator.Calculate(inputs);
            skills = osuSkillRating.ToPlayerSkills();
        }
        else
        {
            using SemaphoreSlim semaphore = new(MaximumParallelCalculations);
            Task<PlayerScoreSkillInput?>[] calculations = scores
                .Select(score => CreateSkillInputWithLimitAsync(score, playmode, semaphore, cancellationToken))
                .ToArray();
            PlayerScoreSkillInput[] inputs = (await Task.WhenAll(calculations))
                .OfType<PlayerScoreSkillInput>()
                .ToArray();

            if (inputs.Length == 0)
                return OsuCardGenerationResult.CalculationFailed(scores.Length);

            skills = _skillCalculator.Calculate(inputs);
        }

        byte[]? avatar = await DownloadAvatarAsync(user.AvatarUrl, cancellationToken);
        using MemoryStream image = _cardGenerator.Generate(new ProfileCardData
        {
            Username = user.Username,
            Mode = (OsuGameMode)(int)playmode,
            Skills = skills,
            Avatar = avatar
        });

        return OsuCardGenerationResult.Success(image.ToArray(), skills, scores.Length, osuSkillRating);
    }

    private async Task<OsuStandardScoreSkillInput?> CreateOsuSkillInputWithLimitAsync(
        Score score,
        SemaphoreSlim semaphore,
        CancellationToken cancellationToken)
    {
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            return await CreateOsuSkillInputAsync(score, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OsuApiUnavailableException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Could not calculate osu!standard skill for beatmap {BeatmapId}",
                score.BeatmapId);
            return null;
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task<PlayerScoreSkillInput?> CreateSkillInputWithLimitAsync(Score score, Playmode playmode,
        SemaphoreSlim semaphore, CancellationToken cancellationToken)
    {
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            return await CreateLegacyModeSkillInputAsync(score, playmode, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OsuApiUnavailableException)
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

    private async Task<OsuStandardScoreSkillInput?> CreateOsuSkillInputAsync(Score score,
        CancellationToken cancellationToken)
    {
        if (score.BeatmapId is null || score.Accuracy is null || score.MaxCombo is null)
            return null;

        using Stream beatmap = await _beatmapsService.DownloadOrCacheBeatmapAsync(score.BeatmapId.Value,
            cancellationToken);
        OfficialScoreCalculation scoreCalculation = await _officialPerformanceHelper.CalculateScoreAsync(
            beatmap,
            score,
            Playmode.Osu,
            calculateCurrent: true,
            calculatePerfect: true,
            cancellationToken: cancellationToken);
        PPCalculationResult? calculation = scoreCalculation.Current;
        PPCalculationResult? perfectCalculation = scoreCalculation.Perfect;

        if (calculation is null || perfectCalculation is null)
            return null;

        if (calculation.DifficultyAttributes is not OfficialOsuDifficultyAttributes osuDifficulty
            || calculation.PerformanceAttributes is not OfficialOsuPerformanceAttributes actualPerformance
            || perfectCalculation.PerformanceAttributes is not OfficialOsuPerformanceAttributes perfectPerformance)
            return null;

        osu.Game.Rulesets.Mods.Mod[] mods = (score.Mods ?? []).ToOsuMods(Playmode.Osu);

        return new OsuStandardScoreSkillInput
        {
            BeatmapId = score.BeatmapId.Value,
            ModSignature = score.Mods is { Length: > 0 }
                ? string.Join("", score.Mods.Select(mod => mod.Acronym?.ToUpperInvariant()).OfType<string>())
                : "NM",
            StarRating = calculation.DifficultyAttributes.StarRating,
            AimDifficulty = osuDifficulty.AimDifficulty,
            AimDifficultSliderCount = osuDifficulty.AimDifficultSliderCount,
            AimDifficultStrainCount = osuDifficulty.AimDifficultStrainCount,
            SpeedDifficulty = osuDifficulty.SpeedDifficulty,
            SpeedNoteCount = osuDifficulty.SpeedNoteCount,
            SpeedDifficultStrainCount = osuDifficulty.SpeedDifficultStrainCount,
            ReadingDifficulty = osuDifficulty.ReadingDifficulty,
            HitCircleCount = osuDifficulty.HitCircleCount,
            SliderCount = osuDifficulty.SliderCount,
            SpinnerCount = osuDifficulty.SpinnerCount,
            TotalHitObjects = calculation.BeatmapHitObjectsCount,
            OverallDifficulty = calculation.OD,
            ApproachRate = calculation.AR,
            ClockRate = OfficialPerformanceHelper.GetClockRate(mods),
            ActualAimPerformance = actualPerformance.Aim,
            ActualSpeedPerformance = actualPerformance.Speed,
            ActualAccuracyPerformance = actualPerformance.Accuracy,
            PerfectAimPerformance = perfectPerformance.Aim,
            PerfectSpeedPerformance = perfectPerformance.Speed,
            PerfectAccuracyPerformance = perfectPerformance.Accuracy
        };
    }

    private async Task<PlayerScoreSkillInput?> CreateLegacyModeSkillInputAsync(Score score, Playmode playmode,
        CancellationToken cancellationToken)
    {
        if (score.BeatmapId is null || score.Accuracy is null || score.MaxCombo is null)
            return null;

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

    private sealed record LegacyBeatmapValues(
        double StarRating,
        double Bpm,
        double CircleSize,
        double ApproachRate,
        double OverallDifficulty,
        double DrainRate);
}

public sealed record OsuCardGenerationResult(
    byte[]? Image,
    PlayerSkills? Skills,
    int RequestedScores,
    OsuCardGenerationFailure Failure,
    OsuSkillRating? SkillRating = null)
{
    public static OsuCardGenerationResult Success(
        byte[] image,
        PlayerSkills skills,
        int requestedScores,
        OsuSkillRating? skillRating = null) =>
        new(image, skills, requestedScores, OsuCardGenerationFailure.None, skillRating);

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
