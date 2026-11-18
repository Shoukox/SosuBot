using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using Newtonsoft.Json;
using osu.Game.Scoring.Legacy;
using SharpCompress.Compressors.LZMA;

namespace SosuBot.Helpers;

/// <summary>What an .osr file says about its score beyond the legacy hit counts OsuParsers reads.</summary>
/// <param name="OnlineId">The legacy score id on the osu! servers; zero or negative for a play never submitted.</param>
/// <param name="LazerScore">The full result osu!lazer stores after the replay frames, when it is there and readable.</param>
public sealed record ReplayFileScore(long? OnlineId, LegacyReplaySoloScoreInfo? LazerScore);

/// <summary>
/// Makes .osr files from osu!lazer readable for OsuParsers, which only understands what osu!stable writes.
/// </summary>
public static class ReplayFileCompatibility
{
    // Header before the life bar: mode (byte), version (int), three strings, six hit counts (ushort),
    // total score (int), max combo (ushort), perfect (byte) and mods (int).
    private const int HitCountsToModsSize = 6 * sizeof(ushort) + sizeof(int) + sizeof(ushort) + sizeof(byte) + sizeof(int);
    private const byte StringPresent = 0x0b;

    private const int FirstVersionWithOnlineId = 20121008;
    private const int FirstVersionWithLongOnlineId = 20140721;
    private const int FirstVersionWithLazerScore = 30000001;

    // An LZMA block starts with its properties (a byte and the dictionary size) and the unpacked size.
    private const int LzmaPropertiesSize = 5;
    private const int LzmaHeaderSize = LzmaPropertiesSize + sizeof(long);

    // Replays come from strangers, and the header decides how much memory unpacking takes. osu!lazer uses a 2 MiB
    // dictionary for about half a kilobyte of JSON.
    private const uint MaximumLzmaDictionaryBytes = 4 * 1024 * 1024;
    private const long MaximumLazerScoreBytes = 64 * 1024;

    /// <summary>
    /// Reads the online id and the full osu!lazer result from the end of a replay. Never throws: whatever is missing
    /// or damaged simply comes back as null.
    /// </summary>
    public static ReplayFileScore ReadScore(byte[] replay)
    {
        long? onlineId = null;
        try
        {
            int version = BinaryPrimitives.ReadInt32LittleEndian(replay.AsSpan(1));
            int position = 1 + sizeof(int);
            for (var i = 0; i < 3; i++)
                position = ReadString(replay, position, out _);
            position = ReadString(replay, position + HitCountsToModsSize, out _);
            position = SkipByteArray(replay, position + sizeof(long), out _);

            if (version >= FirstVersionWithLongOnlineId)
            {
                onlineId = BinaryPrimitives.ReadInt64LittleEndian(replay.AsSpan(position));
                position += sizeof(long);
            }
            else if (version >= FirstVersionWithOnlineId)
            {
                onlineId = BinaryPrimitives.ReadInt32LittleEndian(replay.AsSpan(position));
                position += sizeof(int);
            }

            if (version < FirstVersionWithLazerScore)
                return new ReplayFileScore(onlineId, null);

            SkipByteArray(replay, position, out int blockStart);
            int blockLength = BinaryPrimitives.ReadInt32LittleEndian(replay.AsSpan(position));
            return new ReplayFileScore(onlineId, ReadLazerScore(replay, blockStart, blockLength));
        }
        catch (Exception exception) when (exception is ArgumentOutOfRangeException or IndexOutOfRangeException
                                              or FormatException)
        {
            return new ReplayFileScore(onlineId, null);
        }
    }

    private static LegacyReplaySoloScoreInfo? ReadLazerScore(byte[] replay, int start, int length)
    {
        if (length <= LzmaHeaderSize)
            return null;

        uint dictionary = BinaryPrimitives.ReadUInt32LittleEndian(replay.AsSpan(start + 1));
        long unpackedSize = BinaryPrimitives.ReadInt64LittleEndian(replay.AsSpan(start + LzmaPropertiesSize));
        if (dictionary > MaximumLzmaDictionaryBytes || unpackedSize is <= 0 or > MaximumLazerScoreBytes)
            return null;

        try
        {
            int packedSize = length - LzmaHeaderSize;
            using var packed = new MemoryStream(replay, start + LzmaHeaderSize, packedSize, writable: false);
            using LzmaStream lzma = LzmaStream.Create(replay[start..(start + LzmaPropertiesSize)], packed, packedSize,
                unpackedSize, false);
            using var reader = new StreamReader(lzma, Encoding.UTF8);
            return JsonConvert.DeserializeObject<LegacyReplaySoloScoreInfo>(reader.ReadToEnd());
        }
        catch (Exception)
        {
            // A damaged block fails in many ways inside LZMA and JSON; the replay is still usable without it.
            return null;
        }
    }

    /// <summary>Skips an int32 length and that many bytes, and returns the position after them.</summary>
    private static int SkipByteArray(byte[] data, int position, out int contentStart)
    {
        int length = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(position));
        contentStart = position + sizeof(int);
        if (length > data.Length - contentStart)
            throw new FormatException("byte array is longer than the file");
        return contentStart + Math.Max(length, 0);
    }

    /// <summary>
    /// osu!lazer writes the times of the life bar graph with decimals ("255.63|1"), and OsuParsers reads them with
    /// <see cref="Convert.ToInt32(string)" /> and throws. Returns the file with those times rounded to whole
    /// milliseconds and everything else byte for byte; the file itself when there is nothing to change or it is
    /// not a replay at all, so the decoder can report that as usual.
    /// </summary>
    /// <remarks>Only the copy for the decoder is changed: the renderer gets the original file.</remarks>
    public static byte[] NormalizeLifeBar(byte[] replay)
    {
        try
        {
            int position = 1 + sizeof(int);
            for (var i = 0; i < 3; i++)
                position = ReadString(replay, position, out _);
            position += HitCountsToModsSize;

            int lifeBarStart = position;
            int lifeBarEnd = ReadString(replay, position, out string? lifeBar);
            if (lifeBar is null)
                return replay;

            string normalized = NormalizeLifeBar(lifeBar);
            if (normalized == lifeBar)
                return replay;

            byte[] encoded = EncodeString(normalized);
            var result = new byte[lifeBarStart + encoded.Length + (replay.Length - lifeBarEnd)];
            replay.AsSpan(0, lifeBarStart).CopyTo(result);
            encoded.CopyTo(result, lifeBarStart);
            replay.AsSpan(lifeBarEnd).CopyTo(result.AsSpan(lifeBarStart + encoded.Length));
            return result;
        }
        catch (Exception exception) when (exception is ArgumentOutOfRangeException or IndexOutOfRangeException
                                              or FormatException)
        {
            return replay;
        }
    }

    /// <summary>"255.63|1,2334.24|0.94" becomes "256|1,2334|0.94"; the HP values stay as they are.</summary>
    public static string NormalizeLifeBar(string lifeBar) =>
        string.Join(',', lifeBar.Split(',').Select(point =>
        {
            int separator = point.IndexOf('|');
            if (separator <= 0)
                return point;

            string time = point[..separator];
            return time.Contains('.') &&
                   double.TryParse(time, NumberStyles.Float, CultureInfo.InvariantCulture, out double milliseconds)
                ? Math.Round(milliseconds).ToString("0", CultureInfo.InvariantCulture) + point[separator..]
                : point;
        }));

    /// <summary>Reads an osu! string (0x00, or 0x0b + ULEB128 length + UTF-8) and returns the position after it.</summary>
    private static int ReadString(byte[] data, int position, out string? text)
    {
        byte marker = data[position++];
        if (marker == 0)
        {
            text = null;
            return position;
        }

        if (marker != StringPresent)
            throw new FormatException("not an osu! string");

        int length = 0;
        for (var shift = 0; ; shift += 7)
        {
            byte part = data[position++];
            length |= (part & 0x7f) << shift;
            if (part < 0x80)
                break;
            if (shift > 28)
                throw new FormatException("string length is too long");
        }

        if (position + length > data.Length)
            throw new IndexOutOfRangeException();
        text = Encoding.UTF8.GetString(data, position, length);
        return position + length;
    }

    private static byte[] EncodeString(string text)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        var result = new List<byte>(bytes.Length + 6) { StringPresent };
        uint length = (uint)bytes.Length;
        do
        {
            byte part = (byte)(length & 0x7f);
            length >>= 7;
            result.Add(length == 0 ? part : (byte)(part | 0x80));
        } while (length != 0);

        result.AddRange(bytes);
        return result.ToArray();
    }
}
