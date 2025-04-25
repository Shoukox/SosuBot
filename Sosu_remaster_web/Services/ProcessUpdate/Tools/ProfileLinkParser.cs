using System.Text.RegularExpressions;

namespace Sosu.Services.ProcessUpdate.Tools
{
    public class ProfileLinkParser
    {
        private static readonly Regex linkRegex = new(@"(?>https?:\/\/)?(?>osu|old)\.ppy\.sh\/u(?>sers)?\/(\d+|\S+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        public static ParsedProfile Parse(string text)
        {
            if (string.IsNullOrEmpty(text))
                return null;

            /*
             * [0] - full link
             * [1] - userId
             */
            Match regexMatch = linkRegex.Match(text);

            if (regexMatch.Groups.Count <= 1)
                return null;

            int id = -1;
            if (int.TryParse(regexMatch.Groups[1].Value, out id))
                return new ParsedProfile(id);

            return null;
        }
    }
}
