using System.Text.RegularExpressions;

namespace SosuBot.Helpers
{
    public static class OsuHelper
    {
        public static async Task<byte[]> GetSongPreviewAsync(int beatmapsetId)
        {
            using HttpClient hc = new HttpClient();
            return await hc.GetByteArrayAsync($"https://b.ppy.sh/preview/{beatmapsetId}.mp3");
        }

        private static readonly Regex OsuBeatmapLinkRegex = new(@"(?>https?:\/\/)?(?>osu|old)\.ppy\.sh\/([b,s]|(?>beatmaps)|(?>beatmapsets))\/(\d+)\/?\#?(\w+)?\/?(\d+)?\/?(?>[&,?].+=\w+)?\s?(?>\+(\w+))?", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        public static string? ParseOsuBeatmapLink(string? text, out int? beatmapsetId, out int? beatmapId)
        {
            Match match = OsuBeatmapLinkRegex.Match(text ?? string.Empty);
            if (string.IsNullOrEmpty(match.Value))
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
                    if (int.TryParse(match.Groups[2].Value, out int bId)) beatmapId = bId; else beatmapId = null;
                    break;
                case "s":
                case "beatmapsets":
                    if (int.TryParse(match.Groups[2].Value, out int bsetId)) beatmapsetId = bsetId; else beatmapsetId = null;
                    if (int.TryParse(match.Groups[4].Value, out bId)) beatmapId = bId; else beatmapId = null;
                    break;
                default:
                    beatmapsetId = null;
                    beatmapId = null;
                    break;
            }
            string url = match.Value;
            return string.IsNullOrEmpty(url) ? null : url;
        }

        private static readonly Regex OsuUserLinkRegex = new(@"(?>https?:\/\/)?(?>osu|old)\.ppy\.sh\/u(?>sers)?\/(\d+|\S+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        public static string? ParseOsuUserLink(string text, out int? userId)
        {
            Match match = OsuUserLinkRegex.Match(text ?? string.Empty);
            if (string.IsNullOrEmpty(match.Value))
            {
                userId = null;
                return null;
            }

            string url = match.Value;
            if (int.TryParse(match.Groups[1].Value, out int uId)) userId = uId; else userId = null;
            return string.IsNullOrEmpty(url) ? null : url;
        }
    }
}
