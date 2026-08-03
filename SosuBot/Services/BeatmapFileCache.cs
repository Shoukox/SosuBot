using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SosuBot.Configuration;
using SosuBot.Database.Models;
using System.Globalization;

namespace SosuBot.Services;

public sealed class BeatmapFileCache
{
    private static readonly TimeSpan IndexSaveDebounce = TimeSpan.FromSeconds(10);

    private readonly string _cacheDirectory;
    private readonly BeatmapIndexStore _indexStore;
    private readonly ILogger<BeatmapFileCache> _logger;
    private readonly object _indexLock = new();
    private readonly Dictionary<int, Playmode> _pendingBeatmaps = [];
    private List<int>[]? _beatmapIdsByMode;
    private Task? _delayedIndexSaveTask;
    private bool _indexCanBePersisted;
    private long _indexedDirectoryWriteTimeUtcTicks;
    private int _indexRevision;
    private Task? _indexTask;

    public BeatmapFileCache(IOptions<BeatmapsConfiguration> configuration, ILogger<BeatmapFileCache> logger)
    {
        BeatmapsConfiguration options = configuration.Value;
        _logger = logger;
        _cacheDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(options.CacheDirectory));

        Directory.CreateDirectory(_cacheDirectory);

        string indexPath = string.IsNullOrWhiteSpace(options.IndexFilePath)
            ? $"{_cacheDirectory}.index-v1"
            : options.IndexFilePath;
        _indexStore = new BeatmapIndexStore(indexPath, _cacheDirectory, logger);
    }

    public async Task<byte[]?> TryReadAsync(int beatmapId, CancellationToken cancellationToken = default)
    {
        string path = GetBeatmapPath(beatmapId);
        if (!File.Exists(path)) return null;

        try
        {
            byte[] content = await File.ReadAllBytesAsync(path, cancellationToken);
            return content.Length == 0 ? null : content;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(exception, "Could not read cached beatmap {BeatmapId} from {Path}", beatmapId, path);
            return null;
        }
    }

    public async Task TryStoreAsync(int beatmapId, byte[] content,
        CancellationToken cancellationToken = default)
    {
        if (content.Length == 0) return;

        if (!BeatmapFileMetadata.TryReadPlaymode(content, out Playmode playmode))
        {
            _logger.LogWarning("Refusing to cache invalid beatmap {BeatmapId}", beatmapId);
            return;
        }

        string path = GetBeatmapPath(beatmapId);
        string temporaryPath = Path.Combine(_cacheDirectory, $".{beatmapId}.{Guid.NewGuid():N}.tmp");
        long directoryWriteTimeBeforeStore = GetDirectoryWriteTimeUtcTicks();
        var stored = false;

        try
        {
            await File.WriteAllBytesAsync(temporaryPath, content, cancellationToken);
            File.Move(temporaryPath, path, overwrite: true);
            stored = true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(exception, "Could not cache beatmap {BeatmapId} in {Directory}", beatmapId,
                _cacheDirectory);
        }
        finally
        {
            TryDeleteTemporaryFile(temporaryPath);
        }

        if (stored)
        {
            RegisterBeatmap(beatmapId, playmode, directoryWriteTimeBeforeStore,
                GetDirectoryWriteTimeUtcTicks());
        }
    }

    public Task WarmUpAsync()
    {
        lock (_indexLock)
        {
            if (_indexTask is { IsFaulted: true } or { IsCanceled: true })
            {
                _logger.LogWarning(_indexTask.Exception, "Previous beatmap index build failed; retrying");
                _beatmapIdsByMode = null;
                _indexCanBePersisted = false;
                _indexTask = null;
            }

            if (_beatmapIdsByMode is not null && _indexTask is { IsCompletedSuccessfully: true } &&
                GetDirectoryWriteTimeUtcTicks() != _indexedDirectoryWriteTimeUtcTicks)
            {
                _logger.LogInformation("Beatmap directory changed; refreshing the mode index");
                _beatmapIdsByMode = null;
                _indexCanBePersisted = false;
                _indexTask = null;
            }

            return _indexTask ??= Task.Run(BuildAndPublishBeatmapIndex);
        }
    }

    public Task<long> CountCachedBeatmapFilesAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            try
            {
                _ = File.GetAttributes(_cacheDirectory);
            }
            catch (Exception exception) when (exception is DirectoryNotFoundException or FileNotFoundException)
            {
                return 0L;
            }

            var enumerationOptions = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = false,
                AttributesToSkip = FileAttributes.ReparsePoint
            };

            long count = 0;
            foreach (string path in Directory.EnumerateFiles(_cacheDirectory, "*", enumerationOptions))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.Equals(Path.GetExtension(path), ".osu", StringComparison.OrdinalIgnoreCase))
                    count++;
            }

            return count;
        }, cancellationToken);
    }

    public async Task<int?> GetRandomBeatmapIdAsync(Playmode playmode,
        CancellationToken cancellationToken = default)
    {
        ValidatePlaymode(playmode);
        await WarmUpAsync().WaitAsync(cancellationToken);

        lock (_indexLock)
        {
            List<int> beatmapIds = _beatmapIdsByMode![(int)playmode];
            return beatmapIds.Count == 0 ? null : beatmapIds[Random.Shared.Next(beatmapIds.Count)];
        }
    }

    public async Task FlushIndexAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<int>[]? snapshot = null;
        long directoryWriteTimeUtcTicks = 0;

        lock (_indexLock)
        {
            if (_beatmapIdsByMode is not null && _indexCanBePersisted)
            {
                snapshot = _beatmapIdsByMode
                    .Select(static beatmapIds => (IReadOnlyList<int>)beatmapIds.ToArray())
                    .ToArray();
                directoryWriteTimeUtcTicks = _indexedDirectoryWriteTimeUtcTicks;
            }
        }

        if (snapshot is null || GetDirectoryWriteTimeUtcTicks() != directoryWriteTimeUtcTicks) return;

        await Task.Run(() => _indexStore.TrySave(directoryWriteTimeUtcTicks, snapshot), cancellationToken);
    }

    private BeatmapIndexBuildResult BuildBeatmapIndex()
    {
        List<int>[] beatmapIdsByMode = CreateEmptyIndex();
        var skippedFileNames = 0;
        var invalidBeatmapFiles = 0;
        var unreadableBeatmapFiles = 0;
        var isComplete = true;

        try
        {
            foreach (string path in Directory.EnumerateFiles(_cacheDirectory, "*", SearchOption.TopDirectoryOnly))
            {
                if (!string.Equals(Path.GetExtension(path), ".osu", StringComparison.OrdinalIgnoreCase)) continue;

                if (!int.TryParse(Path.GetFileNameWithoutExtension(path), NumberStyles.None,
                        CultureInfo.InvariantCulture, out int beatmapId) || beatmapId <= 0)
                {
                    skippedFileNames++;
                    continue;
                }

                try
                {
                    if (BeatmapFileMetadata.TryReadPlaymode(path, out Playmode playmode))
                        beatmapIdsByMode[(int)playmode].Add(beatmapId);
                    else
                        invalidBeatmapFiles++;
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    unreadableBeatmapFiles++;
                    isComplete = false;
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(exception, "Could not index beatmaps in {Directory}", _cacheDirectory);
            isComplete = false;
        }

        _logger.LogInformation(
            "Indexed cached beatmaps in {Directory}: {OsuCount} osu, {TaikoCount} taiko, {CatchCount} catch, {ManiaCount} mania",
            _cacheDirectory,
            beatmapIdsByMode[(int)Playmode.Osu].Count,
            beatmapIdsByMode[(int)Playmode.Taiko].Count,
            beatmapIdsByMode[(int)Playmode.Catch].Count,
            beatmapIdsByMode[(int)Playmode.Mania].Count);

        if (skippedFileNames + invalidBeatmapFiles + unreadableBeatmapFiles > 0)
        {
            _logger.LogWarning(
                "Skipped beatmap files while indexing: {InvalidFileNameCount} invalid names, {InvalidBeatmapCount} invalid beatmaps, {UnreadableBeatmapCount} unreadable files",
                skippedFileNames, invalidBeatmapFiles, unreadableBeatmapFiles);
        }

        return new BeatmapIndexBuildResult(beatmapIdsByMode, isComplete);
    }

    private void BuildAndPublishBeatmapIndex()
    {
        long directoryWriteTimeUtcTicks = GetDirectoryWriteTimeUtcTicks();
        bool loadedFromStore = _indexStore.TryLoad(directoryWriteTimeUtcTicks, out List<int>[] beatmapIdsByMode);
        bool indexCanBePersisted = loadedFromStore;

        if (!loadedFromStore)
        {
            BeatmapIndexBuildResult buildResult = BuildBeatmapIndex();
            beatmapIdsByMode = buildResult.BeatmapIdsByMode;
            foreach (List<int> beatmapIds in beatmapIdsByMode)
            {
                beatmapIds.Sort();
                RemoveDuplicateIds(beatmapIds);
            }

            indexCanBePersisted = buildResult.IsComplete &&
                                  GetDirectoryWriteTimeUtcTicks() == directoryWriteTimeUtcTicks;
            if (indexCanBePersisted)
                _indexStore.TrySave(directoryWriteTimeUtcTicks, beatmapIdsByMode);
            else if (!buildResult.IsComplete)
                _logger.LogInformation("Incomplete beatmap mode index was not persisted");
            else
                _logger.LogInformation("Beatmap directory changed while indexing; the mode index was not persisted");
        }

        lock (_indexLock)
        {
            foreach ((int pendingBeatmapId, Playmode playmode) in _pendingBeatmaps)
                RegisterBeatmap(beatmapIdsByMode, pendingBeatmapId, playmode);

            _pendingBeatmaps.Clear();
            _beatmapIdsByMode = beatmapIdsByMode;
            _indexCanBePersisted = indexCanBePersisted;
            _indexedDirectoryWriteTimeUtcTicks = directoryWriteTimeUtcTicks;
        }
    }

    private string GetBeatmapPath(int beatmapId)
    {
        return Path.Combine(_cacheDirectory, $"{beatmapId.ToString(CultureInfo.InvariantCulture)}.osu");
    }

    private void RegisterBeatmap(int beatmapId, Playmode playmode, long directoryWriteTimeBeforeStore,
        long directoryWriteTimeAfterStore)
    {
        lock (_indexLock)
        {
            if (_beatmapIdsByMode is null)
            {
                _pendingBeatmaps[beatmapId] = playmode;
                return;
            }

            RegisterBeatmap(_beatmapIdsByMode, beatmapId, playmode);
            if (_indexCanBePersisted && _indexedDirectoryWriteTimeUtcTicks == directoryWriteTimeBeforeStore)
            {
                _indexedDirectoryWriteTimeUtcTicks = directoryWriteTimeAfterStore;
                ScheduleIndexPersistence();
            }
        }
    }

    private void ScheduleIndexPersistence()
    {
        _indexRevision++;
        if (_delayedIndexSaveTask is null or { IsCompleted: true })
            _delayedIndexSaveTask = Task.Run(PersistIndexAfterQuietPeriodAsync);
    }

    private async Task PersistIndexAfterQuietPeriodAsync()
    {
        while (true)
        {
            int revision;
            lock (_indexLock)
                revision = _indexRevision;

            await Task.Delay(IndexSaveDebounce);

            lock (_indexLock)
            {
                if (revision != _indexRevision) continue;
            }

            await FlushIndexAsync();

            lock (_indexLock)
            {
                if (revision != _indexRevision) continue;
                _delayedIndexSaveTask = null;
                return;
            }
        }
    }

    private static void RegisterBeatmap(List<int>[] beatmapIdsByMode, int beatmapId, Playmode playmode)
    {
        foreach (List<int> beatmapIds in beatmapIdsByMode)
            RemoveSorted(beatmapIds, beatmapId);

        AddSortedUnique(beatmapIdsByMode[(int)playmode], beatmapId);
    }

    private static void AddSortedUnique(List<int> beatmapIds, int beatmapId)
    {
        int index = beatmapIds.BinarySearch(beatmapId);
        if (index < 0) beatmapIds.Insert(~index, beatmapId);
    }

    private static void RemoveSorted(List<int> beatmapIds, int beatmapId)
    {
        int index = beatmapIds.BinarySearch(beatmapId);
        if (index >= 0) beatmapIds.RemoveAt(index);
    }

    private static void RemoveDuplicateIds(List<int> beatmapIds)
    {
        if (beatmapIds.Count < 2) return;

        var uniqueCount = 1;
        for (var index = 1; index < beatmapIds.Count; index++)
        {
            if (beatmapIds[index] == beatmapIds[uniqueCount - 1]) continue;
            beatmapIds[uniqueCount++] = beatmapIds[index];
        }

        if (uniqueCount < beatmapIds.Count)
            beatmapIds.RemoveRange(uniqueCount, beatmapIds.Count - uniqueCount);
    }

    private static List<int>[] CreateEmptyIndex()
    {
        return [[], [], [], []];
    }

    private static void ValidatePlaymode(Playmode playmode)
    {
        if (playmode is < Playmode.Osu or > Playmode.Mania)
            throw new ArgumentOutOfRangeException(nameof(playmode), playmode, "Unknown beatmap playmode.");
    }

    private long GetDirectoryWriteTimeUtcTicks()
    {
        return Directory.GetLastWriteTimeUtc(_cacheDirectory).Ticks;
    }

    private sealed record BeatmapIndexBuildResult(List<int>[] BeatmapIdsByMode, bool IsComplete);

    private void TryDeleteTemporaryFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogDebug(exception, "Could not remove temporary beatmap cache file {Path}", path);
        }
    }
}
