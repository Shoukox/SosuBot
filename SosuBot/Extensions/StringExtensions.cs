using OsuApi.BanchoV2.Models;
using SosuBot.Database.Models;
using System.Web;
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

    /// <summary>
    ///     Gets all args from a string
    /// </summary>
    /// <param name="text">Text</param>
    /// <returns>Array of args</returns>
    public static string[]? GetCommandParameters(this string text)
    {
        if (text.Length == 0 || text[0] != '/') return null;
        return text.Split(' ', StringSplitOptions.TrimEntries)[1..];
    }

    /// <summary>
    ///     Gets all kwargs from a string. Key can only start with a letter
    ///     Examples: mode=osu, mode=1, a=a
    /// </summary>
    /// <param name="text">Text</param>
    /// <returns>Array of kwargs</returns>
    public static string[]? GetCommandKeywordParameters(this string text)
    {
        if (text.Length == 0 || text[0] != '/') return null;
        return text.Split(' ', StringSplitOptions.TrimEntries)[1..]
            .Where(m =>
                m.Split("=") is { } keyvalue
                && keyvalue.Length == 2
                && keyvalue.All(s => s.Length > 0)
                && char.IsLetter(keyvalue[0][0])).ToArray();
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
        string[] possibilitiesOfFruitsInput = ["ctb", "catch", "fruits"];

        // osu ruleset can be also written in some another way
        string[] possibilitiesOfOsuInput = ["osu", "std", "standard", "standart"];

        if (possibilitiesOfFruitsInput.Contains(text)) text = Ruleset.Fruits;
        else if (possibilitiesOfOsuInput.Contains(text)) text = Ruleset.Osu;
        else if (text is not Ruleset.Taiko and not Ruleset.Mania) return null;

        return text;
    }

    /// <summary>
    ///     Tries to convert a <see cref="Ruleset" /> string into a more readable and osu!user friendly version
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
        return ParseMods(text, playmode, out _);
    }

    public static bool TryParseMods(this string text, Playmode playmode, out Mod[] mods)
    {
        mods = ParseMods(text, playmode, out bool isComplete);
        return isComplete;
    }

    private static Mod[] ParseMods(string text, Playmode playmode, out bool isComplete)
    {
        string normalized = text.Trim().ToUpperInvariant();
        if (normalized.StartsWith('+'))
        {
            normalized = normalized[1..];
        }

        Mod[] rulesetMods = playmode switch
        {
            Playmode.Osu => OsuTypesExtensions.AllOsuMods,
            Playmode.Taiko => OsuTypesExtensions.AllTaikoMods,
            Playmode.Catch => OsuTypesExtensions.AllCatchMods,
            Playmode.Mania => OsuTypesExtensions.AllManiaMods,
            _ => throw new NotImplementedException()
        };

        // Ruleset-specific classes do not contain all common lazer mods. For
        // example, ScoreV2, Difficulty Adjust and rate/fun mods are declared
        // in osu.Game itself. Keep the longest acronym first so values such
        // as 10K and SV2 are parsed before their shorter prefixes.
        Mod[] availableMods = rulesetMods
            .Concat(OsuTypesExtensions.AllMods)
            .Where(m => !string.IsNullOrWhiteSpace(m.Acronym))
            .GroupBy(m => m.Acronym, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderByDescending(m => m.Acronym.Length)
            .ToArray();

        var mods = new List<Mod>();
        isComplete = true;
        int position = 0;
        while (position < normalized.Length)
        {
            Mod? currentMod = availableMods.FirstOrDefault(mod =>
                normalized.Length - position >= mod.Acronym.Length &&
                string.Compare(
                    normalized,
                    position,
                    mod.Acronym,
                    0,
                    mod.Acronym.Length,
                    StringComparison.OrdinalIgnoreCase) == 0);

            if (currentMod is null)
            {
                // Preserve the historical ToMods behaviour for callers that
                // intentionally ignore unknown acronyms, while exposing the
                // complete parse result through TryParseMods.
                isComplete = false;
                position++;
                continue;
            }

            mods.Add(currentMod);
            position += currentMod.Acronym.Length;
        }

        return mods.ToArray();
    }
}
