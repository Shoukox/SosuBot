using osu.Game.Rulesets.Catch.Mods;
using osu.Game.Rulesets.Mania.Mods;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Taiko.Mods;
using SosuBot.Database.Database.Models;
using SosuBot.Database.Models;
using SosuBot.Localization;
using System.Text.RegularExpressions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

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

    public static InlineKeyboardMarkup GetRenderSettingsMarkup(DanserConfiguration config, ILocalization language)
    {
        string generalVolume = language.render_menu_generalVolume;
        string musicVolume = language.render_menu_music;
        string sampleVolume = language.render_menu_effects;
        string backgroundDim = language.render_menu_background;
        string skinName = language.render_menu_skin;
        string hitErrorMeter = (config.HitErrorMeter ? Emojis.CheckMarkEmoji : "") + language.render_menu_urBar;
        string aimErrorMeter = (config.AimErrorMeter ? Emojis.CheckMarkEmoji : "") + language.render_menu_aimErrorCircle;
        string motionBlur = (config.MotionBlur ? Emojis.CheckMarkEmoji : "") + language.render_menu_motionBlur;
        string hbBar = (config.HPBar ? Emojis.CheckMarkEmoji : "") + language.render_menu_hpBar;
        string showPP = (config.ShowPP ? Emojis.CheckMarkEmoji : "") + language.render_menu_showPp;
        string hitCounter = (config.HitCounter ? Emojis.CheckMarkEmoji : "") + language.render_menu_hitCounter;
        string ignoreFails = (config.IgnoreFailsInReplays ? Emojis.CheckMarkEmoji : "") + language.render_menu_ignoreFails;
        string video = (config.Video ? Emojis.CheckMarkEmoji : "") + language.render_menu_video;
        string storyboard = (config.Storyboard ? Emojis.CheckMarkEmoji : "") + language.render_menu_storyboard;
        string mods = (config.Mods ? Emojis.CheckMarkEmoji : "") + language.render_menu_mods;
        string keyOverlay = (config.KeyOverlay ? Emojis.CheckMarkEmoji : "") + language.render_menu_keys;
        string combo = (config.Combo ? Emojis.CheckMarkEmoji : "") + language.render_menu_combo;
        string leaderboard = (config.Leaderboard ? Emojis.CheckMarkEmoji : "") + language.render_menu_leaderboard;
        string strainGraph = (config.StrainGraph ? Emojis.CheckMarkEmoji : "") + language.render_menu_strainGraph;
        string useExperimentalRenderer = (config.UseExperimentalRenderer ? Emojis.CheckMarkEmoji : "") + language.render_menu_useExperimentalRenderer;
        string resetSettings = language.render_menu_resetSettings;
        var ikm = new InlineKeyboardMarkup(
            [
                [
                    InlineKeyboardButton.WithCallbackData($"{generalVolume}: {config.GeneralVolume*100:00}%", $"rs general-volume")
                ],
                [
                    InlineKeyboardButton.WithCallbackData($"{musicVolume}: {config.MusicVolume*100:00}%", $"rs music-volume"),
                    InlineKeyboardButton.WithCallbackData($"{sampleVolume}: {config.SampleVolume*100:00}%", $"rs effects-volume")
                ],
                [
                    InlineKeyboardButton.WithCallbackData($"{backgroundDim}: {config.BackgroundDim*100:00}%", $"rs background")
                ],
                [
                    InlineKeyboardButton.WithCallbackData($"{skinName}: {config.SkinName}", $"rs skin 1")
                ],
                [
                    InlineKeyboardButton.WithCallbackData(hitErrorMeter, $"rs hit-error-meter"),
                    InlineKeyboardButton.WithCallbackData(aimErrorMeter, $"rs aim-error-meter"),
                    InlineKeyboardButton.WithCallbackData(motionBlur, $"rs motion-blur"),
                ],
                [
                    InlineKeyboardButton.WithCallbackData(hbBar, $"rs hp-bar"),
                    InlineKeyboardButton.WithCallbackData(showPP, $"rs pp"),
                    InlineKeyboardButton.WithCallbackData(hitCounter, $"rs hit-counter"),
                ],
                [
                    InlineKeyboardButton.WithCallbackData(ignoreFails, $"rs ignore-fails"),
                    InlineKeyboardButton.WithCallbackData(video, $"rs video"),
                    InlineKeyboardButton.WithCallbackData(storyboard, $"rs storyboard"),
                ],
                [
                    InlineKeyboardButton.WithCallbackData(mods, $"rs mods"),
                    InlineKeyboardButton.WithCallbackData(keyOverlay, $"rs key-overlay"),
                    InlineKeyboardButton.WithCallbackData(combo, $"rs combo"),
                ],
                [
                    InlineKeyboardButton.WithCallbackData(leaderboard, $"rs leaderboard"),
                    InlineKeyboardButton.WithCallbackData(strainGraph, $"rs strain-graph"),
                ],
                [
                    InlineKeyboardButton.WithCallbackData(useExperimentalRenderer, $"rs experimental-renderer"),
                ],
                [
                    InlineKeyboardButton.WithCallbackData(resetSettings, $"rs reset-settings"),
                ],
            ]
        );

        return ikm;
    }
    public static InputFile GetBeatmapCoverPhotoAsInputFile(int beatmapsetId) => new InputFileUrl(new Uri(string.Format(OsuConstants.BeatmapsCover, beatmapsetId.ToString())));
}
