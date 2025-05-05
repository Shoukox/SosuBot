using System.Text.RegularExpressions;

namespace SosuBot.Localization
{
    public static class Extensions
    {
        public static string Fill(this string text, IEnumerable<string> replace)
        {
            foreach (var item in replace)
            {
                int ind = Regex.Match(text, @"{(.*)}").Index;
                text = text.Remove(ind, 2).Insert(ind, item);
            }
            return text;
        }
    }
}
