using FFmpeg.AutoGen;
using Microsoft.Extensions.Logging;
using OsuApi;
using OsuApi.V2;
using OsuApi.V2.Users.Models;
using Polly;
using SosuBot.Caching;
using SosuBot.Helpers.OutputText;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types;

namespace SosuBot.Helpers
{
    public static class RedisHelper
    {
        public static async Task<InputFile> GetOrCacheBeatmapsetCover(int beatmapsetId, RedisCaching redis, ILogger? logger = null, TimeSpan? timeSpan = null)
        {
            TimeSpan defaultTimeSpan = TimeSpan.FromHours(4);

            string key = $"beatmapsetcover:{beatmapsetId}";
            InputFile cover;
            if (await redis.GetAsync<InputFile>(key) is not { } cachedCover)
            {
                logger?.LogInformation($"Downloading cover for {beatmapsetId}");
                cover = OsuHelper.GetBeatmapCoverPhotoAsInputFile(beatmapsetId);
                await redis.SetAsync(key, cover, timeSpan ?? defaultTimeSpan);
            }
            else
            {
                logger?.LogInformation($"Getting a cached cover for {beatmapsetId}");
                cover = cachedCover;
            }

            return cover;
        }

        public static async Task<BeatmapExtended> GetOrCacheBeatmap(int beatmapId, Api api, RedisCaching redis, ILogger? logger = null, TimeSpan? timeSpan = null)
        {
            if (api.CurrentApiVersion() == ApiVersion.ApiV1) throw new NotSupportedException();
            ApiV2 osuApiV2 = (ApiV2)api;

            string key = $"beatmap:{beatmapId}";
            BeatmapExtended beatmap;
            if (await redis.GetAsync<BeatmapExtended>(key) is not { } cachedBeatmap)
            {
                logger?.LogInformation($"Getting beatmap infos ({beatmapId}) via osuApi");
                var getBeatmapResponse = await osuApiV2.Beatmaps.GetBeatmap(beatmapId);
                beatmap = getBeatmapResponse!.BeatmapExtended!;
                await redis.SetAsync(key, beatmap, timeSpan);
            }
            else
            {
                logger?.LogInformation($"Getting a cached beatmap for {beatmapId}");
                beatmap = cachedBeatmap;
            }

            return beatmap;
        }

        public static async Task<BeatmapsetExtended> GetOrCacheBeatmapset(int beatmapsetId, Api api, RedisCaching redis, ILogger? logger = null, TimeSpan? timeSpan = null)
        {
            if (api.CurrentApiVersion() == ApiVersion.ApiV1) throw new NotSupportedException();
            ApiV2 osuApiV2 = (ApiV2)api;

            string key = $"beatmapset:{beatmapsetId}";
            BeatmapsetExtended beatmapset;
            if (await redis.GetAsync<BeatmapsetExtended>(key) is not { } cachedBeatmapset)
            {
                logger?.LogInformation($"Getting beatmapset infos ({beatmapsetId}) via osuApi");
                beatmapset = await osuApiV2.Beatmapsets.GetBeatmapset(beatmapsetId);
                await redis.SetAsync(key, beatmapset, timeSpan);
            }
            else
            {
                logger?.LogInformation($"Getting a cached beatmapset for {beatmapsetId}");
                beatmapset = cachedBeatmapset;
            }

            return beatmapset;
        }
    }
}
