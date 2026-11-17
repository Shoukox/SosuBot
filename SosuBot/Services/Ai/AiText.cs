using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace SosuBot.Services.Ai;

/// <summary>
/// Turns osu! wiki markdown and news HTML into short plain text for the model: no images, link targets, front
/// matter or markup, and never longer than the budget a tool result may spend.
/// </summary>
public static partial class AiText
{
    /// <summary>The most characters one long text (a wiki page, a news post) may put into a tool result.</summary>
    public const int MaxLongText = 6_000;

    /// <summary>One heading of a wiki page and the plain text under it, up to the next heading of any level.</summary>
    public sealed record Section(int Level, string Title, string Text);

    /// <summary>Plain text of osu! wiki markdown, with headings kept as lines starting with #.</summary>
    public static string CleanWikiMarkdown(string markdown)
    {
        string text = markdown.Replace("\r\n", "\n");
        text = FrontMatter().Replace(text, "");
        text = HtmlComment().Replace(text, "");
        text = LinkDefinition().Replace(text, "");
        text = Image().Replace(text, "");
        text = InlineLink().Replace(text, "$1");
        text = ReferenceLink().Replace(text, "$1");
        text = Footnote().Replace(text, "");
        text = HeadingAttributes().Replace(text, "");
        text = text.Replace("**", "").Replace("__", "").Replace("`", "");
        text = HtmlTag().Replace(text, "");
        return CollapseBlankLines(text);
    }

    /// <summary>Splits cleaned wiki text at its headings; text before the first heading is a level-0 section.</summary>
    public static IReadOnlyList<Section> SplitSections(string cleanedText)
    {
        var sections = new List<Section>();
        int level = 0;
        string title = "";
        var body = new StringBuilder();

        foreach (string line in cleanedText.Split('\n'))
        {
            Match heading = Heading().Match(line);
            if (heading.Success)
            {
                Flush();
                level = heading.Groups[1].Length;
                title = heading.Groups[2].Value.Trim();
                continue;
            }

            body.Append(line).Append('\n');
        }

        Flush();
        return sections;

        void Flush()
        {
            string content = body.ToString().Trim();
            if (content.Length > 0 || title.Length > 0)
                sections.Add(new Section(level, title, content));
            body.Clear();
        }
    }

    /// <summary>
    /// The section whose title matches <paramref name="name" /> (case-insensitive, exact first, then containing),
    /// with its subsections; <c>null</c> when there is none.
    /// </summary>
    public static string? FindSection(IReadOnlyList<Section> sections, string name)
    {
        string wanted = name.Trim().TrimStart('#').Trim();
        int index = IndexOf(s => s.Title.Equals(wanted, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
            index = IndexOf(s => s.Title.Contains(wanted, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
            return null;

        Section found = sections[index];
        var text = new StringBuilder(found.Text);
        for (int i = index + 1; i < sections.Count && sections[i].Level > found.Level; i++)
            text.Append("\n\n").Append(new string('#', sections[i].Level)).Append(' ').Append(sections[i].Title)
                .Append('\n').Append(sections[i].Text);
        return text.ToString().Trim();

        int IndexOf(Func<Section, bool> predicate)
        {
            for (var i = 0; i < sections.Count; i++)
                if (sections[i].Level > 0 && predicate(sections[i]))
                    return i;
            return -1;
        }
    }

    /// <summary>Plain text of an HTML fragment, with paragraphs, list items and headings on their own lines.</summary>
    public static string HtmlToText(string html)
    {
        string text = html.Replace("\r\n", "\n");
        text = ScriptOrStyle().Replace(text, "");
        text = HtmlComment().Replace(text, "");
        text = BlockBreak().Replace(text, "\n");
        text = ListItem().Replace(text, "\n- ");
        text = HtmlTag().Replace(text, "");
        text = WebUtility.HtmlDecode(text).Replace(' ', ' ');
        text = SpaceRun().Replace(text, " ");
        return CollapseBlankLines(text);
    }

    /// <summary>Cuts <paramref name="text" /> to <paramref name="maxLength" /> characters at a word boundary.</summary>
    public static string Truncate(string text, int maxLength = MaxLongText)
    {
        if (text.Length <= maxLength)
            return text;

        int cut = text.LastIndexOfAny([' ', '\n'], maxLength - 1, Math.Min(200, maxLength));
        return text[..(cut > 0 ? cut : maxLength)].TrimEnd() + " …(обрезано)";
    }

    private static string CollapseBlankLines(string text)
    {
        string trimmedLines = string.Join('\n', text.Split('\n').Select(line => line.TrimEnd()));
        return BlankLines().Replace(trimmedLines, "\n\n").Trim();
    }

    [GeneratedRegex(@"\A---\n.*?\n---\n", RegexOptions.Singleline)]
    private static partial Regex FrontMatter();

    [GeneratedRegex(@"<!--.*?-->", RegexOptions.Singleline)]
    private static partial Regex HtmlComment();

    [GeneratedRegex(@"^\s*\[[^\]\n]+\]:\s*\S+.*$", RegexOptions.Multiline)]
    private static partial Regex LinkDefinition();

    [GeneratedRegex(@"!\[[^\]\n]*\]\([^)\n]*\)|!\[[^\]\n]*\]\[[^\]\n]*\]")]
    private static partial Regex Image();

    [GeneratedRegex(@"\[([^\]\n]+)\]\([^)\n]*\)")]
    private static partial Regex InlineLink();

    [GeneratedRegex(@"\[([^\]\n]+)\]\[[^\]\n]*\]")]
    private static partial Regex ReferenceLink();

    [GeneratedRegex(@"\[\^[^\]\n]+\]")]
    private static partial Regex Footnote();

    [GeneratedRegex(@"[ \t]*\{(?:[#.]|id=)[^}\n]*\}[ \t]*$", RegexOptions.Multiline)]
    private static partial Regex HeadingAttributes();

    [GeneratedRegex(@"^(#{1,6})\s+(.+)$")]
    private static partial Regex Heading();

    [GeneratedRegex(@"<(script|style)\b.*?</\1>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex ScriptOrStyle();

    [GeneratedRegex(@"<br\s*/?>|</?(p|div|h[1-6]|ul|ol|table|tr|blockquote|figure)\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex BlockBreak();

    [GeneratedRegex(@"<li\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex ListItem();

    // Only real tags: a "<" followed by a letter, so "AR < 5" in wiki text survives.
    [GeneratedRegex(@"</?[a-zA-Z][^<>]*>")]
    private static partial Regex HtmlTag();

    [GeneratedRegex(@"[ \t]{2,}")]
    private static partial Regex SpaceRun();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex BlankLines();
}
