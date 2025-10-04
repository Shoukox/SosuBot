using OsuApi.V2;
using OsuApi.V2.Models;
using SosuBot.Extensions;
using SosuBot.Helpers.Types;
using SosuBot.Helpers.Types.Statistics;
using SosuBot.Localization;
using SosuBot.Localization.Languages;
using Mod = OsuApi.V2.Models.Mod;

namespace SosuBot.Helpers.OutputText;

public static class ScoreHelper
{
    public static string GetModsText(Mod[] mods)
    {
        var modsText = "+" + string.Join("", mods.Select(m =>
        {
            var speedChangeString = "";
            if (m.Settings?.SpeedChange.HasValue ?? false) speedChangeString = $"({m.Settings.SpeedChange:0.00}x)";
            return m.Acronym + speedChangeString;
        }));
        if (modsText == "+") modsText += "NM";
        return modsText;
    }

    public static string GetFormattedPpTextConsideringNull(double? scorePp, string format = "N2")
    {
        var ppText = scorePp?.ToString(format) ?? "—";
        return ppText;
    }

    public static string GetScoreStatisticsText(ScoreStatistics scoreStatistics, Playmode playmode)
    {
        var scoreStatisticsText = string.Empty;
        switch (playmode)
        {
            case Playmode.Osu:
            case Playmode.Taiko:
                scoreStatisticsText +=
                    $"{scoreStatistics.Great}/{scoreStatistics.Ok}/{scoreStatistics.Meh}";
                break;
            case Playmode.Catch:
                scoreStatisticsText +=
                    $"{scoreStatistics.Great}/{scoreStatistics.Ok}/{scoreStatistics.SmallTickHit}/{scoreStatistics.SmallTickMiss}";
                break;
            case Playmode.Mania:
                scoreStatisticsText +=
                    $"{scoreStatistics.Perfect}/{scoreStatistics.Great}/{scoreStatistics.Good}/{scoreStatistics.Ok}/{scoreStatistics.Meh}";
                break;
        }

        return scoreStatisticsText;
    }

    /// <summary>
    /// Gets a suitable emoji for this mode
    /// </summary>
    /// <param name="playmode"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public static string GetPlaymodeEmoji(Playmode playmode)
    {
        return playmode switch
        {
            Playmode.Osu => "🔵",
            Playmode.Taiko => "🥁",
            Playmode.Catch => "🍎",
            Playmode.Mania => "🎹",
            _ => throw new NotImplementedException()
        };
    }

    /// <summary>
    ///     Get an emoji for the score rank (X, XH, S)
    /// </summary>
    /// <param name="scoreRank">Score rank from API</param>
    /// <param name="passed">Whether the score was passed</param>
    /// <returns></returns>
    public static string GetScoreRankEmoji(string? scoreRank, bool passed = true)
    {
        if (scoreRank == null) return string.Empty;

        scoreRank = scoreRank.ToUpperInvariant();

        if (!passed) scoreRank = "F";

        return scoreRank switch
        {
            "XH" => "⚪️",
            "X" => "🟡",
            "SH" => "⚪️",
            "S" => "🟡",
            "A" => "🟢",
            "B" => "🔵",
            "C" => "🟠",
            "D" => "🔴",
            "F" => "🟣",
            _ => ""
        };
    }

    /// <summary>
    ///     Replaces X by SS
    /// </summary>
    /// <returns></returns>
    public static string ParseScoreRank(string scoreRank)
    {
        return scoreRank.Replace("X", "SS");
    }

    public static string GetScoreUrl(long scoreId)
    {
        return $"{OsuConstants.BaseScoreUrl}{scoreId}";
    }

    public static string GetScoreUrlWrappedInString(long scoreId, string text)
    {
        return $"<a href=\"{GetScoreUrl(scoreId)}\">{text}</a>";
    }

    public static async Task<string> GetDailyStatisticsSendText(Playmode playmode, DailyStatistics dailyStatistics, ApiV2 osuApi)
    {
        ILocalization language = new Russian();

        var activePlayers = dailyStatistics.ActiveUsers;
        var passedScores = dailyStatistics.Scores.Where(m => m.ModeInt == (int)playmode).ToList();

        var usersAndTheirScores = activePlayers.Select(m =>
            {
                return (m, passedScores.Where(s => s.UserId == m.Id).ToArray());
            })
            .Where(m => m.Item2.Length != 0)
            .OrderByDescending(m => m.Item2.Length)
            .ToArray();

        var mostPlayedBeatmaps = passedScores
            .GroupBy(m => m.BeatmapId!.Value)
            .OrderByDescending(m => m.Count())
            .ToArray();

        var topPpScores = "";
        var count = 0;
        foreach (var us in usersAndTheirScores.OrderByDescending(pair => pair.Item2.Max(m => m.Pp)))
        {
            if (count >= 5) break;

            string modeEmoji = GetPlaymodeEmoji(playmode);
            var scoresOrderedByPp = us.Item2
                .GroupBy(score => score.BeatmapId)
                .Select(m => m.MaxBy(s => s.Pp))
                .OrderByDescending(m => m!.Pp)
                .Select(m => GetScoreUrlWrappedInString(m!.Id!.Value, $"{GetFormattedPpTextConsideringNull(m.Pp, format: "N0")}pp{modeEmoji}"))
                .ToArray();

            var topPpScoresTextForCurrentUser = string.Join(", ", scoresOrderedByPp.Take(3));
            topPpScores +=
                $"{count + 1}. <b>{UserHelper.GetUserProfileUrlWrappedInUsernameString(us.m.Id.Value, us.m.Username!)}</b> — <i>{topPpScoresTextForCurrentUser}</i>\n";
            count += 1;
        }
        if (string.IsNullOrEmpty(topPpScores))
        {
            topPpScores = ":(";
        }

        var topActivePlayers = "";
        count = 0;
        foreach (var us in usersAndTheirScores)
        {
            if (count >= 5) break;
            topActivePlayers +=
                $"{count + 1}. <b>{UserHelper.GetUserProfileUrlWrappedInUsernameString(us.m.Id.Value, us.m.Username!)}</b> — {us.Item2.Length} скоров, макс. <i>{GetFormattedPpTextConsideringNull(us.Item2.Max(m => m.Pp), format: "N0")}pp💪</i>\n";
            count += 1;
        }
        if (string.IsNullOrEmpty(topActivePlayers))
        {
            topActivePlayers = ":(";
        }
        
        var topMostPlayedBeatmaps = "";
        count = 0;
        foreach (var us in mostPlayedBeatmaps)
        {
            if (count >= 5) break;

            if (!dailyStatistics.CachedBeatmapsFromOsuApi.TryGetValue(us.Key, out var beatmap))
            {
                beatmap = (await osuApi.Beatmaps.GetBeatmap(us.Key))!.BeatmapExtended;
                dailyStatistics.CachedBeatmapsFromOsuApi[us.Key] = beatmap!;
            }

            if (!dailyStatistics.CachedBeatmapsetsFromOsuApi.TryGetValue(us.Key, out var beatmapsetExtended))
            {
                beatmapsetExtended =
                    await osuApi.Beatmapsets.GetBeatmapset(beatmap!.BeatmapsetId.Value);
                dailyStatistics.CachedBeatmapsetsFromOsuApi[us.Key] = beatmapsetExtended;
            }

            topMostPlayedBeatmaps +=
                $"{count + 1}. (<b>{beatmap!.DifficultyRating}⭐️</b>) <a href=\"https://osu.ppy.sh/beatmaps/{beatmap.Id}\">{beatmapsetExtended.Title.EncodeHtml()} [{beatmap.Version.EncodeHtml()}]</a> — <b>{us.Count()} скоров</b>\n";
            count += 1;
        }
        if (string.IsNullOrEmpty(topMostPlayedBeatmaps))
        {
            topMostPlayedBeatmaps = ":(";
        }

        var activePlayersCount = usersAndTheirScores.Length;
        var passedScoresCount = passedScores.Count;
        var beatmapsPlayed = usersAndTheirScores.SelectMany(m => m.Item2).DistinctBy((m) => m.BeatmapId).Count();

        var tashkentNow = dailyStatistics.DayOfStatistic.ChangeTimezone(Country.Uzbekistan);
        var sendText = language.send_dailyStatistic.Fill([
            $"{tashkentNow:dd.MM.yyyy HH:mm} (по тшк.)",
            $"{activePlayersCount}",
            $"{passedScoresCount}",
            $"{beatmapsPlayed}",
            $"{topPpScores}\n",
            $"{topActivePlayers}\n",
            $"{topMostPlayedBeatmaps}\n"
        ]);

        return sendText;
    }
}