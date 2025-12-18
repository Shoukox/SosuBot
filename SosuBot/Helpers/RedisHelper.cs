using Microsoft.Extensions.Logging;
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
                cover = OsuHelper.GetBeatmapCoverPhotoAsInputFile(beatmapsetId);
                await redis.SetAsync(key, cover, timeSpan ?? defaultTimeSpan);
                logger?.LogInformation($"Downloading cover for {beatmapsetId}");
            }
            else
            {
                logger?.LogInformation($"Getting a cached cover for {beatmapsetId}");
                cover = cachedCover;
            }

            return cover;
        }
    }
}
