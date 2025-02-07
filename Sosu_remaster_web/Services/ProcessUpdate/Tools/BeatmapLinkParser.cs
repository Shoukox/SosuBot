using OppaiSharp;
using System.Text.RegularExpressions;

namespace Sosu.Services.ProcessUpdate.Tools
{
    public class BeatmapLinkParser
    {
        private static readonly Regex linkRegex = new(@"(?>https?:\/\/)?(?>osu|old)\.ppy\.sh\/([b,s]|(?>beatmaps)|(?>beatmapsets))\/(\d+)\/?\#?(\w+)?\/?(\d+)?\/?(?>[&,?].+=\w+)?\s?(?>\+(\w+))?", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static ParsedBeatmap Parse(string text)
        {
            if (string.IsNullOrEmpty(text))
                return null;

            /*
             * [0] - full link
             * [1] - beatmapsets\beatmap\b\s
             * [2] - beatmapsetId\beatmapId
             * [3] - mode
             * [4] - beatmapId if isBeatmapset
             * [5] - modes
             */

            Match regexMatch = linkRegex.Match(text);

            if (regexMatch.Groups.Count <= 1)
                return null;

            var regexGroups = regexMatch.Groups.Values.ToArray();
            bool isBeatmapset = regexGroups[1].Value == "beatmapsets" || regexGroups[1].Value == "s";
            bool isBeatmap = regexGroups[1].Value == "beatmaps" || regexGroups[1].Value == "b";
            bool isModes = (regexGroups[5].Value != "");

            Mods mods = Mods.NoMod;
            if (isModes)
            {
                mods = ConvertMods(regexGroups[5].Value);
            }

            int id = -1;
            if (isBeatmapset && int.TryParse(regexGroups[2].Value, out id))
            {
                bool condition = (regexGroups.Length >= 4 && int.TryParse(regexGroups[4].Value, out _));
                return new ParsedBeatmap((regexGroups.Length >= 4 && int.TryParse(regexGroups[4].Value, out _)) ? int.Parse(regexGroups[4].Value) : id, !condition, mods);
            }
            if (isBeatmap && int.TryParse(regexGroups[2].Value, out id))
            {
                return new ParsedBeatmap(id, false, mods);
            }

            return null;
        }
        private static Mods ConvertMods(string modsString)
        {
            Mods mods = Mods.NoMod;
            for (int i = 0; i <= modsString.Length - 1; i += 2)
            {
                string mod = modsString.Substring(i, 2).ToUpper();
                mods = (Mods)Variables.osuApi.getMod((osu.V1.Enums.Mods)mods, ref mod);

            }
            return mods;
        }
    }
}
