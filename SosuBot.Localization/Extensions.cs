using System.Text.RegularExpressions;

namespace SosuBot.Localization;

public static class Extensions
{
    private static readonly Regex PlaceholderRegex = new(@"\{.*?\}", RegexOptions.Compiled);

    public static string Fill(this string text, IEnumerable<string> replace)
    {
        if (text is null) throw new ArgumentNullException(nameof(text));
        if (replace is null) throw new ArgumentNullException(nameof(replace));

        using var e = replace.GetEnumerator();
        return PlaceholderRegex.Replace(text, m => e.MoveNext() ? e.Current ?? string.Empty : m.Value);
    }
}