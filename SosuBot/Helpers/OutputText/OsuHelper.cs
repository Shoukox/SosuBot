using osu.Game.Rulesets.Catch.Mods;
using osu.Game.Rulesets.Mania.Mods;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Taiko.Mods;
using SosuBot.Database.Models;
using System.Text.RegularExpressions;
using Telegram.Bot.Types;

namespace SosuBot.Helpers.OutputText;

public static partial class OsuHelper
{
    [GeneratedRegex(@"(?>https?:\/\/)?(?>osu|old)\.ppy\.sh\/([b,s]|(?>beatmaps)|(?>beatmapsets))\/(\d+)\/?\#?(\w+)?\/?(\d+)?\/?(?>[&,?].+=\w+)?\s?(?>\+(\w+))?(-)?", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex OsuBeatmapLinkRegex();


    [GeneratedRegex(@"(?>https?:\/\/)?(?>osu|old)\.ppy\.sh\/u(?>sers)?\/(\d+|\S+)(-)?", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex OsuUserLinkRegex();

    [GeneratedRegex(@"https?:\/\/(?:old\.|new\.)?osu\.ppy\.sh\/(?:ss|scores)\/(\w+\/\d+|\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex OsuScoreLinkRegex();

    public static async Task<byte[]?> GetSongPreviewAsync(int beatmapsetId)
    {
        try
        {
            using var hc = new HttpClient();
            return await hc.GetByteArrayAsync($"https://b.ppy.sh/preview/{beatmapsetId}.mp3");
        }
        catch
        {
            return null;
        }
    }

    public static string? ParseOsuBeatmapLink(IEnumerable<string>? urls, out int? beatmapsetId, out int? beatmapId)
    {
        if (urls == null)
        {
            beatmapsetId = null;
            beatmapId = null;
            return null;
        }

        Match? match = null;
        foreach (var url in urls)
        {
            match = OsuBeatmapLinkRegex().Match(url);
            if (match.Success) break;
        }

        if (match == null || !match.Success)
        {
            beatmapsetId = null;
            beatmapId = null;
            return null;
        }

        switch (match.Groups[1].Value)
        {
            case "b":
            case "beatmaps":
                beatmapsetId = null;
                if (int.TryParse(match.Groups[2].Value, out var bId)) beatmapId = bId;
                else beatmapId = null;
                break;
            case "s":
            case "beatmapsets":
                if (int.TryParse(match.Groups[2].Value, out var bsetId)) beatmapsetId = bsetId;
                else beatmapsetId = null;
                if (int.TryParse(match.Groups[4].Value, out bId)) beatmapId = bId;
                else beatmapId = null;
                break;
            default:
                beatmapsetId = null;
                beatmapId = null;
                break;
        }

        return match.Value;
    }

    public static string? ParseOsuUserLink(IEnumerable<string>? urls, out int? userId)
    {
        if (urls == null)
        {
            userId = null;
            return null;
        }

        Match? match = null;
        foreach (var url in urls)
        {
            match = OsuUserLinkRegex().Match(url);
            if (match.Success) break;
        }

        if (match == null || !match.Success)
        {
            userId = null;
            return null;
        }

        if (int.TryParse(match.Groups[1].Value, out var uId)) userId = uId;
        else userId = null;
        return match.Value;
    }

    public static string? ParseOsuScoreLink(IEnumerable<string>? urls, out long? scoreId)
    {
        if (urls == null)
        {
            scoreId = null;
            return null;
        }

        Match? match = null;
        foreach (var url in urls)
        {
            match = OsuScoreLinkRegex().Match(url);
            if (match.Success) break;
        }

        if (match == null || !match.Success)
        {
            scoreId = null;
            return null;
        }

        if (long.TryParse(match.Groups[1].Value, out long uId)) scoreId = uId;
        else scoreId = null;
        return match.Value;
    }

    public static Mod GetClassicMode(Playmode playmode)
    {
        return playmode switch
        {
            Playmode.Osu => new OsuModClassic(),
            Playmode.Taiko => new TaikoModClassic(),
            Playmode.Catch => new CatchModClassic(),
            Playmode.Mania => new ManiaModClassic(),
            _ => throw new NotImplementedException()
        };
    }

    public static InputFile GetBeatmapCoverPhotoAsInputFile(int beatmapsetId) => new InputFileUrl(new Uri(string.Format(OsuConstants.BeatmapsCover, beatmapsetId.ToString())));
}