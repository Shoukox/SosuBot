using SosuBot.Database.Models;
using System.Globalization;
using System.Text;

namespace SosuBot.Services;

internal static class BeatmapFileMetadata
{
    private const int MaxHeaderCharacterCount = 64 * 1024;

    public static bool TryReadPlaymode(string path, out Playmode playmode)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete, bufferSize: 512, FileOptions.SequentialScan);
        return TryReadPlaymode(stream, out playmode);
    }

    public static bool TryReadPlaymode(byte[] content, out Playmode playmode)
    {
        using var stream = new MemoryStream(content, writable: false);
        return TryReadPlaymode(stream, out playmode);
    }

    private static bool TryReadPlaymode(Stream stream, out Playmode playmode)
    {
        playmode = Playmode.Osu;

        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true,
            bufferSize: 512, leaveOpen: false);

        const string formatPrefix = "osu file format v";
        string? formatLine = reader.ReadLine();
        if (formatLine is null || formatLine.Length > MaxHeaderCharacterCount)
        {
            return false;
        }

        ReadOnlySpan<char> format = formatLine.AsSpan().Trim().TrimStart('\uFEFF');
        if (!format.StartsWith(formatPrefix, StringComparison.Ordinal) ||
            !int.TryParse(format[formatPrefix.Length..], NumberStyles.None, CultureInfo.InvariantCulture,
                out int formatVersion) || formatVersion <= 0)
        {
            return false;
        }

        int charactersRead = formatLine.Length;
        var insideGeneralSection = false;
        var sawGeneralSection = false;

        while (reader.ReadLine() is { } line)
        {
            charactersRead += line.Length + 1;
            if (charactersRead > MaxHeaderCharacterCount) return false;

            ReadOnlySpan<char> trimmedLine = line.AsSpan().Trim();
            if (trimmedLine.Length == 0) continue;

            if (trimmedLine[0] == '[' && trimmedLine[^1] == ']')
            {
                if (trimmedLine.Equals("[General]", StringComparison.OrdinalIgnoreCase))
                {
                    insideGeneralSection = true;
                    sawGeneralSection = true;
                    continue;
                }

                if (insideGeneralSection) return true;
                continue;
            }

            if (!insideGeneralSection) continue;

            int separatorIndex = trimmedLine.IndexOf(':');
            if (separatorIndex < 0 ||
                !trimmedLine[..separatorIndex].Trim().Equals("Mode", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            ReadOnlySpan<char> value = trimmedLine[(separatorIndex + 1)..].Trim();
            if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int rawPlaymode) ||
                rawPlaymode is < (int)Playmode.Osu or > (int)Playmode.Mania)
            {
                return false;
            }

            playmode = (Playmode)rawPlaymode;
            return true;
        }

        // Mode is optional in legacy .osu files and defaults to osu!standard.
        return sawGeneralSection;
    }
}
