using SosuBot.Database.Models;
using SosuBot.Extensions;

namespace SosuBot.Helpers;

public static class OsuCommandArguments
{
    public sealed record Parsed(IReadOnlyList<string> Parameters, Playmode? Playmode);

    public sealed record Player(string? Username, Playmode? Playmode);

    public static string[] GetParameters(string messageText)
    {
        return (messageText.GetCommandParameters() ?? [])
            .Where(parameter => !string.IsNullOrWhiteSpace(parameter))
            .ToArray();
    }

    public static Parsed? Parse(string messageText)
    {
        List<string> parameters = GetParameters(messageText).ToList();
        Playmode? playmode = null;

        List<string> keywordModes = parameters
            .Where(parameter => parameter.StartsWith("mode=", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (keywordModes.Count > 1)
            return null;

        if (keywordModes.Count == 1)
        {
            if (!TryParsePlaymode(keywordModes[0], out Playmode parsedKeywordMode))
                return null;

            playmode = parsedKeywordMode;
            parameters.Remove(keywordModes[0]);
        }

        List<(int Index, Playmode Playmode)> standaloneModes = [];
        for (var i = 0; i < parameters.Count; i++)
        {
            if (TryParsePlaymode(parameters[i], out Playmode standalonePlaymode))
                standaloneModes.Add((i, standalonePlaymode));
        }

        if (standaloneModes.Count > 1 || (standaloneModes.Count == 1 && playmode is not null))
            return null;

        if (standaloneModes.Count == 1)
        {
            (int index, Playmode standalonePlaymode) = standaloneModes[0];
            playmode = standalonePlaymode;
            parameters.RemoveAt(index);
        }

        if (parameters.Any(parameter => parameter.Contains('=')))
            return null;

        return new Parsed(parameters, playmode);
    }

    public static Player? ParsePlayer(string messageText)
    {
        Parsed? parsed = Parse(messageText);
        if (parsed is null)
            return null;

        string username = JoinUsername(parsed.Parameters);
        return new Player(username.Length == 0 ? null : username, parsed.Playmode);
    }

    public static string JoinUsername(IEnumerable<string> parameters)
    {
        return string.Join(' ', parameters.Where(parameter => !string.IsNullOrWhiteSpace(parameter))).Trim();
    }

    public static bool TrySplitPlayers(
        Parsed parsed,
        out string? firstUsername,
        out string? secondUsername)
    {
        List<string> parameters = parsed.Parameters.ToList();
        int separatorIndex = parameters.FindIndex(parameter => parameter == "|");
        if (separatorIndex >= 0)
        {
            if (parameters.LastIndexOf("|") != separatorIndex)
            {
                firstUsername = null;
                secondUsername = null;
                return false;
            }

            firstUsername = JoinUsername(parameters[..separatorIndex]);
            secondUsername = JoinUsername(parameters[(separatorIndex + 1)..]);
            return firstUsername.Length > 0 && secondUsername.Length > 0;
        }

        if (parameters.Count != 2)
        {
            firstUsername = null;
            secondUsername = null;
            return false;
        }

        firstUsername = parameters[0];
        secondUsername = parameters[1];
        return true;
    }

    private static bool TryParsePlaymode(string parameter, out Playmode playmode)
    {
        string? ruleset = parameter.ParseToRuleset();
        if (ruleset is not null)
        {
            playmode = ruleset.ParseRulesetToPlaymode();
            return true;
        }

        string normalizedParameter = parameter.Trim().ToLowerInvariant();
        playmode = normalizedParameter switch
        {
            "o" => Playmode.Osu,
            "t" => Playmode.Taiko,
            "c" => Playmode.Catch,
            "m" => Playmode.Mania,
            _ => default
        };

        return normalizedParameter is "o" or "t" or "c" or "m";
    }
}
