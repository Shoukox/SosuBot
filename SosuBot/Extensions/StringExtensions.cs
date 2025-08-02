using System.Web;
using OsuApi.V2.Models;
using SosuBot.Helpers.Types;
using Mod = osu.Game.Rulesets.Mods.Mod;

namespace SosuBot.Extensions;

public static class StringExtensions
{
    public static string RemoveUsernamePostfix(this string text, string username)
    {
        return text.Replace($"@{username}", "");
    }

    public static bool IsCommand(this string text)
    {
        return text.Length > 0 && text[0] == '/';
    }

    public static string GetCommand(this string text)
    {
        text = text.Trim();
        var spaceIndex = text.IndexOf(' ');
        if (spaceIndex == -1) return text;
        return text[..spaceIndex];
    }

    public static string[]? GetCommandParameters(this string text)
    {
        if (text.Length == 0 || text[0] != '/') return null;
        return text.Split(' ', StringSplitOptions.TrimEntries)[1..];
    }

    /// <summary>
    ///     Tries to convert the user's input into a <see cref="Ruleset" /> string
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    public static string? ParseToRuleset(this string text)
    {
        text = text.Trim().ToLowerInvariant().Replace("mode=", "");

        // user can type taiko/mania, but fruits can be written in another way
        string[] possibilitiesOfFruitsInput = ["ctb", "catch"];

        // osu ruleset can be also written in some another way
        string[] possibilitiesOfOsuInput = ["osu", "std", "standard", "standart"];

        if (possibilitiesOfFruitsInput.Contains(text)) text = Ruleset.Fruits;
        else if (possibilitiesOfOsuInput.Contains(text)) text = Ruleset.Osu;
        else if (text is not Ruleset.Taiko and not Ruleset.Mania) return null;

        return text;
    }

    /// <summary>
    ///     Tries to convert a <see cref="Ruleset" /> string into a more readable and user friendly version
    /// </summary>
    /// <param name="ruleset"></param>
    /// <returns></returns>
    public static string ParseRulesetToGamemode(this string ruleset)
    {
        return ruleset switch
        {
            Ruleset.Osu => "osu!std",
            Ruleset.Mania => "osu!mania",
            Ruleset.Taiko => "osu!taiko",
            Ruleset.Fruits => "osu!catch",
            _ => throw new NotImplementedException()
        };
    }

    public static Playmode ParseRulesetToPlaymode(this string ruleset)
    {
        return ruleset switch
        {
            Ruleset.Osu => Playmode.Osu,
            Ruleset.Taiko => Playmode.Taiko,
            Ruleset.Fruits => Playmode.Catch,
            Ruleset.Mania => Playmode.Mania,
            _ => throw new NotImplementedException()
        };
    }

    public static string? EncodeHtml(this string? text)
    {
        return HttpUtility.HtmlEncode(text);
    }

    public static Mod[] ToMods(this string text, Playmode playmode)
    {
        text = text.Trim().ToUpperInvariant();

        var startFrom = 0;
        if (!char.IsAsciiLetter(text[0])) startFrom = 1;

        var rulesetMods = playmode switch
        {
            Playmode.Osu => OsuTypesExtensions.AllOsuMods,
            Playmode.Taiko => OsuTypesExtensions.AllTaikoMods,
            Playmode.Catch => OsuTypesExtensions.AllCatchMods,
            Playmode.Mania => OsuTypesExtensions.AllManiaMods,
            _ => throw new NotImplementedException()
        };

        var mods = new List<Mod>();
        for (var i = startFrom; i < text.Length; i += 2)
        {
            var currentMod = rulesetMods.FirstOrDefault(m => m.Acronym.ToUpperInvariant() == text.Substring(i, 2));
            if (currentMod == null) continue;
            mods.Add(currentMod);
        }

        return mods.ToArray();
    }
}