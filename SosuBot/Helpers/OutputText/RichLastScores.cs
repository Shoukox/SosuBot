using System.Text;
using SosuBot.Localization;

namespace SosuBot.Helpers.OutputText;

/// <summary>
///     One score of /last, already formatted. Text values are HTML-encoded where they come from osu! users.
/// </summary>
public sealed record LastScoreEntry(
    int? GlobalRank,
    string RankText,
    int BeatmapId,
    int BeatmapsetId,
    string TitleHtml,
    string VersionHtml,
    string Status,
    string StarsText,
    string HitsText,
    int? Misses,
    string AccuracyText,
    string ModsText,
    int? Combo,
    string MaxComboText,
    string PpText,
    string IfFcText,
    string ScoreUrl,
    string EndedAgoText,
    string CompletionText,
    bool TooManyObjects);

/// <summary>
///     Renders /lw as a Telegram rich message (https://core.telegram.org/bots/api#rich-message-formatting-options):
///     a beatmap cover per score, a stats table and link buttons.
/// </summary>
public static class RichLastScores
{
    /// <summary>Keeps the message well inside the 50-media and 500-block limits.</summary>
    public const int MaxScores = 10;

    public static string BuildHtml(
        ILocalization language,
        int userId,
        string usernameHtml,
        string gamemode,
        IReadOnlyList<LastScoreEntry> entries)
    {
        var html = new StringBuilder();
        html.Append($"<h3><a href=\"{UserHelper.GetUserProfileUrl(userId)}\">{usernameHtml}</a> · {gamemode}</h3>");

        for (var i = 0; i < entries.Count; i++)
        {
            LastScoreEntry entry = entries[i];
            if (i > 0) html.Append("<hr/>");

            string counter = entries.Count > 1 ? $"{i + 1}. " : "";
            string cover = string.Format(OsuConstants.BeatmapsCover, entry.BeatmapsetId);
            html.Append($"<figure><img src=\"{cover}\"/><figcaption>{counter}<b>{entry.TitleHtml}</b> [{entry.VersionHtml}]" +
                        $"<cite>{entry.Status} · {entry.StarsText}⭐</cite></figcaption></figure>");

            html.Append("<table bordered compact><tr><th>Rank</th><th>PP</th><th>Acc</th><th>Combo</th></tr>" +
                        $"<tr><td align=\"center\">{entry.RankText}</td>" +
                        $"<td align=\"center\"><b>{entry.PpText}</b></td>" +
                        $"<td align=\"center\">{entry.AccuracyText}%</td>" +
                        $"<td align=\"center\">{entry.Combo}x/{entry.MaxComboText}x</td></tr></table>");

            string globalRank = entry.GlobalRank is { } rank ? $"<mark>Global #{rank}</mark> · " : "";
            html.Append($"<p>{globalRank}➕<b>{entry.ModsText}</b> · {entry.HitsText}/{entry.Misses}❌ · " +
                        $"{entry.CompletionText}% {language.richLast_completion}<br/><i>{entry.IfFcText}</i></p>");

            if (entry.TooManyObjects)
                html.Append($"<p><i>{language.last_tooManyObjectsLimitedInfo}</i></p>");

            html.Append("<tg-button-row align=\"left\">" +
                        $"<tg-button type=\"url\" url=\"{entry.ScoreUrl}\">{language.richLast_scoreButton}</tg-button>" +
                        $"<tg-button type=\"url\" url=\"https://osu.ppy.sh/beatmaps/{entry.BeatmapId}\">{language.richLast_beatmapButton}</tg-button>" +
                        "</tg-button-row>");

            // The /render hint of /last is omitted: /render reads score links from message text, which a rich
            // message does not have.
            html.Append($"<footer>{entry.EndedAgoText}</footer>");
        }

        return html.ToString();
    }
}
