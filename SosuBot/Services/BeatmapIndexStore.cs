using Microsoft.Extensions.Logging;
using System.Text;

namespace SosuBot.Services;

internal sealed class BeatmapIndexStore
{
    private const uint Magic = 0x53424958; // SBIX
    private const int FormatVersion = 1;
    private const int PlaymodeCount = 4;

    private readonly string _beatmapDirectory;
    private readonly string _indexPath;
    private readonly ILogger _logger;

    public BeatmapIndexStore(string indexPath, string beatmapDirectory, ILogger logger)
    {
        _indexPath = Path.GetFullPath(indexPath);
        _beatmapDirectory = beatmapDirectory;
        _logger = logger;
    }

    public bool TryLoad(long directoryWriteTimeUtcTicks, out List<int>[] beatmapIdsByMode)
    {
        beatmapIdsByMode = CreateEmptyIndex();
        if (!File.Exists(_indexPath)) return false;

        try
        {
            using var stream = new FileStream(_indexPath, FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize: 64 * 1024, FileOptions.SequentialScan);
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);

            if (reader.ReadUInt32() != Magic || reader.ReadInt32() != FormatVersion ||
                !string.Equals(reader.ReadString(), _beatmapDirectory, StringComparison.Ordinal) ||
                reader.ReadInt64() != directoryWriteTimeUtcTicks)
            {
                return false;
            }

            for (var playmode = 0; playmode < PlaymodeCount; playmode++)
            {
                int count = reader.ReadInt32();
                long remainingIds = (stream.Length - stream.Position) / sizeof(int);
                if (count < 0 || count > remainingIds)
                    throw new InvalidDataException("Beatmap index contains an invalid bucket length.");

                var beatmapIds = new List<int>(count);
                var previousBeatmapId = 0;
                for (var index = 0; index < count; index++)
                {
                    int beatmapId = reader.ReadInt32();
                    if (beatmapId <= previousBeatmapId)
                        throw new InvalidDataException("Beatmap index IDs must be positive, sorted and unique.");

                    beatmapIds.Add(beatmapId);
                    previousBeatmapId = beatmapId;
                }

                beatmapIdsByMode[playmode] = beatmapIds;
            }

            if (stream.Position != stream.Length)
                throw new InvalidDataException("Beatmap index contains trailing data.");

            _logger.LogInformation("Loaded cached beatmap mode index from {IndexPath}", _indexPath);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                                InvalidDataException or EndOfStreamException or FormatException)
        {
            _logger.LogWarning(exception, "Could not load beatmap mode index from {IndexPath}; rebuilding it",
                _indexPath);
            beatmapIdsByMode = CreateEmptyIndex();
            return false;
        }
    }

    public void TrySave(long directoryWriteTimeUtcTicks, IReadOnlyList<int>[] beatmapIdsByMode)
    {
        string? indexDirectory = Path.GetDirectoryName(_indexPath);
        if (indexDirectory is null) return;

        string temporaryPath = Path.Combine(indexDirectory, $".{Path.GetFileName(_indexPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            Directory.CreateDirectory(indexDirectory);
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                       bufferSize: 64 * 1024, FileOptions.SequentialScan))
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(Magic);
                writer.Write(FormatVersion);
                writer.Write(_beatmapDirectory);
                writer.Write(directoryWriteTimeUtcTicks);

                foreach (IReadOnlyList<int> beatmapIds in beatmapIdsByMode)
                {
                    writer.Write(beatmapIds.Count);
                    foreach (int beatmapId in beatmapIds)
                        writer.Write(beatmapId);
                }

                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, _indexPath, overwrite: true);
            _logger.LogInformation("Saved beatmap mode index to {IndexPath}", _indexPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(exception, "Could not save beatmap mode index to {IndexPath}", _indexPath);
        }
        finally
        {
            TryDeleteTemporaryFile(temporaryPath);
        }
    }

    private void TryDeleteTemporaryFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogDebug(exception, "Could not remove temporary beatmap index file {Path}", path);
        }
    }

    private static List<int>[] CreateEmptyIndex()
    {
        return [[], [], [], []];
    }
}
