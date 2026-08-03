using Microsoft.Extensions.Logging;
using SosuBot.Database.Models;
using System.Globalization;

namespace SosuBot.Services;

public sealed class BeatmapsService
{
    public const string HttpClientName = "BeatmapsServiceHttpClient";

    private static readonly DownloadSource[] DownloadSources =
    [
        new("osu!", new Uri("https://osu.ppy.sh/osu/")),
        new("syui", new Uri("https://syui.eternityglow.de/osu/")),
        new("mino", new Uri("https://catboy.best/osu/"))
    ];

    private readonly BeatmapFileCache _cache;
    private readonly HttpClient _httpClient;
    private readonly ILogger<BeatmapsService> _logger;

    public BeatmapsService(IHttpClientFactory httpClientFactory, BeatmapFileCache cache,
        ILogger<BeatmapsService> logger)
    {
        _cache = cache;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient(HttpClientName);
    }

    public Task<int?> GetRandomCachedBeatmapIdAsync(Playmode playmode,
        CancellationToken cancellationToken = default)
    {
        return _cache.GetRandomBeatmapIdAsync(playmode, cancellationToken);
    }

    public async Task<Stream> DownloadOrCacheBeatmapAsync(int beatmapId,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(beatmapId);

        byte[]? cachedBeatmap = await _cache.TryReadAsync(beatmapId, cancellationToken);
        if (cachedBeatmap is not null)
        {
            if (BeatmapFileMetadata.TryReadPlaymode(cachedBeatmap, out _))
            {
                _logger.LogInformation("Loaded beatmap {BeatmapId} from the filesystem cache", beatmapId);
                return CreateReadOnlyStream(cachedBeatmap);
            }

            _logger.LogWarning("Ignoring invalid cached beatmap {BeatmapId}", beatmapId);
        }

        List<Exception> failures = [];
        foreach (DownloadSource source in DownloadSources)
        {
            try
            {
                byte[] downloadedBeatmap = await DownloadBeatmapAsync(source, beatmapId, cancellationToken);
                await _cache.TryStoreAsync(beatmapId, downloadedBeatmap, cancellationToken);
                _logger.LogInformation("Downloaded beatmap {BeatmapId} from {Source}", beatmapId, source.Name);
                return CreateReadOnlyStream(downloadedBeatmap);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                failures.Add(exception);
                _logger.LogWarning(exception, "Could not download beatmap {BeatmapId} from {Source}", beatmapId,
                    source.Name);
            }
        }

        throw new AggregateException($"Could not download beatmap {beatmapId} from any configured source.",
            failures);
    }

    private async Task<byte[]> DownloadBeatmapAsync(DownloadSource source, int beatmapId,
        CancellationToken cancellationToken)
    {
        Uri requestUri = new(source.BaseUri, beatmapId.ToString(CultureInfo.InvariantCulture));
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        using HttpResponseMessage response = await _httpClient.SendAsync(request,
            HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        byte[] content = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        if (!BeatmapFileMetadata.TryReadPlaymode(content, out _))
            throw new InvalidDataException($"{source.Name} returned content that is not an .osu file.");

        return content;
    }

    private static Stream CreateReadOnlyStream(byte[] content)
    {
        return new MemoryStream(content, writable: false);
    }

    private sealed record DownloadSource(string Name, Uri BaseUri);
}
