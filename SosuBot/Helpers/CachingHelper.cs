using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using OsuApi;
using OsuApi.V2;
using OsuApi.V2.Models;
using OsuApi.V2.Users.Models;
using SosuBot.Helpers.OutputText;
using Telegram.Bot.Types;

namespace SosuBot.Helpers
{
    public class CachingHelper(HybridCache cache, ILogger<CachingHelper> logger)
    {
        public async Task<InputFile> GetOrCacheBeatmapsetCover(int beatmapsetId)
        {
            string key = $"beatmapsetcover:{beatmapsetId}";
            InputFile cover = await cache.GetOrCreateAsync(
                key,
                async token =>
                {
                    logger?.LogInformation("Downloading cover for {beatmapsetId}", beatmapsetId);
                    return OsuHelper.GetBeatmapCoverPhotoAsInputFile(beatmapsetId);
                },
                options: new HybridCacheEntryOptions
                {
                    Expiration = TimeSpan.FromHours(24)
                }
            );

            return cover;
        }

        public async Task<BeatmapExtended?> GetOrCacheBeatmap(int beatmapId, Api api)
        {
            ApiV2 osuApiV2 = (ApiV2)api;

            string key = $"beatmap:{beatmapId}";

            try
            {
                return await cache.GetOrCreateAsync(
                    key,
                    async token =>
                    {
                        logger?.LogInformation($"Getting beatmap infos ({beatmapId}) via osuApi");
                        var response = await osuApiV2.Beatmaps.GetBeatmap(beatmapId);
                        return response?.BeatmapExtended ?? throw new InvalidOperationException($"Beatmap {beatmapId} not found.");
                    },
                    options: new HybridCacheEntryOptions { Expiration = TimeSpan.FromHours(1) }
                );
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        public async Task<BeatmapsetExtended?> GetOrCacheBeatmapset(int beatmapsetId, Api api)
        {
            ApiV2 osuApiV2 = (ApiV2)api;

            string key = $"beatmapset:{beatmapsetId}";

            try
            {

                return await cache.GetOrCreateAsync(
                    key,
                    async token =>
                    {
                        logger?.LogInformation($"Getting beatmap infos ({beatmapsetId}) via osuApi");
                        var response = await osuApiV2.Beatmapsets.GetBeatmapset(beatmapsetId);
                        return response ?? throw new InvalidOperationException($"Beatmapset {beatmapsetId} not found.");
                    },
                    options: new HybridCacheEntryOptions { Expiration = TimeSpan.FromHours(1) }
                );
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        public async Task<Score?> GetOrCacheScore(long scoreId, Api api)
        {
            ApiV2 osuApiV2 = (ApiV2)api;

            string key = $"score:{scoreId}";

            try
            {
                return await cache.GetOrCreateAsync(
                    key,
                    async token =>
                    {
                        logger?.LogInformation($"Getting score infos ({scoreId}) via osuApi");
                        var response = await osuApiV2.Scores.GetScore(scoreId);
                        return response ?? throw new InvalidOperationException($"Score {scoreId} not found.");
                    },
                    options: new HybridCacheEntryOptions { Expiration = TimeSpan.FromDays(7) }
                );
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }
    }
}
