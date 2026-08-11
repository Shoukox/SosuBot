using Microsoft.Extensions.Logging;
using OsuApi.BanchoV2;
using OsuApi.BanchoV2.Clients.Beatmaps.HttpIO;
using OsuApi.BanchoV2.Clients.Users.HttpIO;
using OsuApi.BanchoV2.Models;
using OsuApi.BanchoV2.Users.Models;
using SosuBot.Graphics;
using SosuBot.Graphics.Models;
using SosuBot.Helpers;

namespace SosuBot.Services;

public sealed class VideoPreviewService
{
    private const int MaximumAvatarBytes = 5 * 1024 * 1024;
    private const int MaximumBackgroundBytes = 15 * 1024 * 1024;
    private const int MaximumFlagBytes = 1024 * 1024;

    private readonly BanchoApiV2 _osuApi;
    private readonly CachingHelper _cachingHelper;
    private readonly ScorePreviewGenerator _generator;
    private readonly HttpClient _httpClient;
    private readonly ILogger<VideoPreviewService> _logger;

    public VideoPreviewService(
        BanchoApiV2 osuApi,
        CachingHelper cachingHelper,
        ScorePreviewGenerator generator,
        IHttpClientFactory httpClientFactory,
        ILogger<VideoPreviewService> logger)
    {
        _osuApi = osuApi;
        _cachingHelper = cachingHelper;
        _generator = generator;
        _httpClient = httpClientFactory.CreateClient("CustomHttpClient");
        _logger = logger;
    }

    public async Task<VideoPreviewGenerationResult> GenerateAsync(long scoreId, ScorePreviewText text,
        CancellationToken cancellationToken = default)
    {
        try
        {
            Score? score = await _cachingHelper.GetOrCacheScore(scoreId, _osuApi)
                .WaitAsync(cancellationToken);
            if (score is null)
                return VideoPreviewGenerationResult.ScoreNotFound();

            if (score.UserId is null || score.BeatmapId is null)
                return VideoPreviewGenerationResult.MissingData();

            BeatmapExtended? beatmap = await _cachingHelper.GetOrCacheBeatmap(score.BeatmapId.Value, _osuApi,
                cancellationToken);
            if (beatmap?.Id is null || beatmap.BeatmapsetId is null || string.IsNullOrWhiteSpace(beatmap.Version))
                return VideoPreviewGenerationResult.MissingData();

            BeatmapsetExtended? beatmapset = await _cachingHelper.GetOrCacheBeatmapset(
                    beatmap.BeatmapsetId.Value, _osuApi)
                .WaitAsync(cancellationToken);
            if (beatmapset is null || string.IsNullOrWhiteSpace(beatmapset.Title))
                return VideoPreviewGenerationResult.MissingData();

            GetUserResponse? userResponse = await _osuApi.Users.GetUser(
                score.UserId.Value.ToString(),
                new GetUserQueryParameters(),
                cancellationToken: cancellationToken);
            UserExtend? user = userResponse?.UserExtend;
            if (user is null || string.IsNullOrWhiteSpace(user.Username))
                return VideoPreviewGenerationResult.MissingData();

            GetBeatmapAttributesResponse? attributes = await _osuApi.Beatmaps.GetBeatmapAttributes(
                beatmap.Id.Value,
                new GetBeatmapAttributesRequest { Mods = score.Mods ?? [] },
                cancellationToken);

            string[] mods = (score.Mods ?? [])
                .Select(mod => mod.Acronym?.ToUpperInvariant())
                .OfType<string>()
                .ToArray();
            double bpm = ApplyClockRate(beatmap.BPM ?? 0, mods);
            double starRating = attributes?.DifficultyAttributes?.StarRating ??
                                score.Beatmap?.DifficultyRating ?? beatmap.DifficultyRating ?? 0;

            Task<byte[]?> backgroundTask = DownloadFirstAvailableImageAsync(
                [GetBeatmapBackgroundUrl(beatmap.BeatmapsetId.Value), beatmapset.Covers?.Cover2x],
                MaximumBackgroundBytes,
                cancellationToken);
            Task<byte[]?> avatarTask = DownloadImageAsync(user.AvatarUrl, MaximumAvatarBytes, cancellationToken);
            Task<byte[]?> flagTask = DownloadImageAsync(GetFlagUrl(user.CountryCode), MaximumFlagBytes,
                cancellationToken);
            await Task.WhenAll(backgroundTask, avatarTask, flagTask);

            using MemoryStream preview = _generator.Generate(new ScorePreviewData
            {
                BeatmapTitle = beatmapset.Title,
                DifficultyName = beatmap.Version,
                Username = user.Username,
                Rank = score.Rank ?? "F",
                BackgroundImage = await backgroundTask,
                AvatarImage = await avatarTask,
                CountryFlagImage = await flagTask,
                CountryCode = user.CountryCode,
                CountryRank = user.Statistics?.CountryRank,
                IsFullCombo = score.IsPerfectCombo ?? false,
                Misses = score.Statistics?.Miss ?? 0,
                PerformancePoints = score.Pp,
                Combo = score.MaxCombo ?? 0,
                AccuracyPercent = (score.Accuracy ?? 0) * 100,
                StarRating = starRating,
                Bpm = bpm,
                Mods = mods
            }, text);

            return VideoPreviewGenerationResult.Success(preview.ToArray());
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
            _logger.LogError(exception, "Could not generate video preview for score {ScoreId}", scoreId);
            return VideoPreviewGenerationResult.Failed();
        }
    }

    private async Task<byte[]?> DownloadImageAsync(string? imageUrl, int maximumBytes,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            return null;

        if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out Uri? uri))
            uri = new Uri(new Uri("https://osu.ppy.sh"), imageUrl);

        try
        {
            using HttpResponseMessage response = await _httpClient.GetAsync(uri,
                HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > maximumBytes)
                return null;

            byte[] content = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            return content.Length <= maximumBytes ? content : null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Could not download score-preview image from {ImageUrl}", uri);
            return null;
        }
    }

    private async Task<byte[]?> DownloadFirstAvailableImageAsync(IEnumerable<string?> imageUrls, int maximumBytes,
        CancellationToken cancellationToken)
    {
        foreach (string? imageUrl in imageUrls.Where(url => !string.IsNullOrWhiteSpace(url)))
        {
            byte[]? image = await DownloadImageAsync(imageUrl, maximumBytes, cancellationToken);
            if (image is not null)
                return image;
        }

        return null;
    }

    private static double ApplyClockRate(double bpm, IReadOnlyCollection<string> mods)
    {
        if (mods.Any(mod => mod is "DT" or "NC"))
            return bpm * 1.5;
        if (mods.Any(mod => mod is "HT" or "DC"))
            return bpm * 0.75;
        return bpm;
    }

    private static string? GetFlagUrl(string? countryCode) => string.IsNullOrWhiteSpace(countryCode)
        ? null
        : $"https://osu.ppy.sh/images/flags/{countryCode.ToUpperInvariant()}.png";

    private static string GetBeatmapBackgroundUrl(int beatmapsetId) =>
        $"https://assets.ppy.sh/beatmaps/{beatmapsetId}/covers/raw.jpg";
}

public enum VideoPreviewGenerationFailure
{
    None,
    ScoreNotFound,
    MissingData,
    Failed
}

public sealed record VideoPreviewGenerationResult(byte[]? Image, VideoPreviewGenerationFailure Failure)
{
    public static VideoPreviewGenerationResult Success(byte[] image) =>
        new(image, VideoPreviewGenerationFailure.None);

    public static VideoPreviewGenerationResult ScoreNotFound() =>
        new(null, VideoPreviewGenerationFailure.ScoreNotFound);

    public static VideoPreviewGenerationResult MissingData() =>
        new(null, VideoPreviewGenerationFailure.MissingData);

    public static VideoPreviewGenerationResult Failed() =>
        new(null, VideoPreviewGenerationFailure.Failed);
}
