using Microsoft.Extensions.Logging;

namespace SosuBot.Services
{
    public class BeatmapsService
    {
        private static HttpClient HttpClient { get; } = new();

        private const string BaseUrlMino = "https://catboy.best/";
        private const string BaseUrlSyui = "https://syui.eternityglow.de/";
        private const string BaseUrlOsu = "http://osu.ppy.sh/";
        private static string CacheDirectory = Path.Combine(AppContext.BaseDirectory, "cache", "beatmaps");

        private readonly ILogger<BeatmapsService> _logger;

        public BeatmapsService(ILogger<BeatmapsService> logger)
        {
            _logger = logger;

            Directory.CreateDirectory(CacheDirectory);
        }

        public async Task<Stream> DownloadOrCacheBeatmap(int beatmapId)
        {
            Result<Stream> downloadResult;

            string cachePath = Path.Combine(CacheDirectory, $"{beatmapId}.osu");
            if (File.Exists(cachePath))
            {
                using var fs = new FileStream(cachePath, FileMode.Open, FileAccess.Read);

                Stream stream = new MemoryStream();
                fs.CopyTo(stream);
                stream.Position = 0;

                downloadResult = Result<Stream>.FromSuccess(stream);

                _logger.LogInformation($"Got beatmap cache for {beatmapId} from the filesystem");
            }
            else
            {
                downloadResult = await DownloadBeatmapViaOsu(beatmapId);
                if (downloadResult.Success) _logger.LogInformation($"Got beatmap cache for {beatmapId} from osu");
            }

            if (!downloadResult.Success)
            {
                downloadResult = await DownloadBeatmapViaSyui(beatmapId);
                if (downloadResult.Success) _logger.LogInformation($"Got beatmap cache for {beatmapId} from syui");
            }

            if (!downloadResult.Success)
            {
                downloadResult = await DownloadBeatmapViaMino(beatmapId);
                if (downloadResult.Success) _logger.LogInformation($"Got beatmap cache for {beatmapId} from mino");
            }

            // cache in filesystem if success
            if (downloadResult.Success && !File.Exists(cachePath))
            {
                using var fs = new FileStream(cachePath, FileMode.Create, FileAccess.Write);
                downloadResult.Output!.CopyTo(fs);
                fs.Flush();
                downloadResult.Output!.Position = 0;
                if (downloadResult.Success) _logger.LogInformation($"Saving beatmap cache for {beatmapId} in the filesystem");
            }

            return downloadResult.Output!;
        }

        /// <summary>
        /// NEEDS OSU_SESSION COOKIE
        /// </summary>
        /// <param name="beatmapsetId"></param>
        /// <returns></returns>
        private async Task<Result<Stream>> DownloadBeatmapViaOsu(int beatmapId)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, BaseUrlOsu + $"osu/{beatmapId}");
                var response = await HttpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                return Result<Stream>.FromSuccess(await response.Content.ReadAsStreamAsync());
            }
            catch (Exception e)
            {
                return Result<Stream>.FromFailure(e);
            }
        }

        private async Task<Result<Stream>> DownloadBeatmapViaSyui(int beatmapId)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, BaseUrlSyui + $"osu/{beatmapId}");
                var response = await HttpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                return Result<Stream>.FromSuccess(await response.Content.ReadAsStreamAsync());
            }
            catch (Exception e)
            {
                return Result<Stream>.FromFailure(e);
            }
        }

        private async Task<Result<Stream>> DownloadBeatmapViaMino(int beatmapId)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, BaseUrlMino + $"osu/{beatmapId}");
                var response = await HttpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                return Result<Stream>.FromSuccess(await response.Content.ReadAsStreamAsync());
            }
            catch (Exception e)
            {
                return Result<Stream>.FromFailure(e);
            }
        }

        public enum Source
        {
            Osu = 0,
            Mino = 1,
            Syui = 2
        }

        public record Result<T>
        {
            public bool Success { get; init; }
            public T? Output { get; init; }
            public Exception? Exception { get; init; }

            public static Result<T> FromSuccess(T output) => new Result<T>
            {
                Success = true,
                Output = output,
                Exception = null
            };

            public static Result<T> FromFailure(Exception exception) => new Result<T>
            {
                Success = false,
                Output = default,
                Exception = exception
            };
        }
    }
}
