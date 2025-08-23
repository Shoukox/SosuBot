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
                    $"{scoreStatistics.Great}/{scoreStatistics.LargeTickHit}/{scoreStatistics.SmallTickHit}/{scoreStatistics.SmallTickMiss}xKatu";
                break;
            case Playmode.Mania:
                scoreStatisticsText +=
                    $"{scoreStatistics.Perfect}/{scoreStatistics.Great}/{scoreStatistics.Good}/{scoreStatistics.Ok}/{scoreStatistics.Meh}";
                break;
        }

        return scoreStatisticsText;
    }

    public static string GetScoreUrl(long scoreId)
    {
        return $"{OsuConstants.BaseScoreUrl}{scoreId}";
    }

    public static string GetScoreUrlWrappedInString(long scoreId, string text)
    {
        return $"<a href=\"{GetScoreUrl(scoreId)}\">{text}</a>";
    }

    public static async Task<string> GetDailyStatisticsSendText(DailyStatistics dailyStatistics, ApiV2 osuApi)
    {
        ILocalization language = new Russian();
        var activePlayersCount = dailyStatistics.ActiveUsers.Count;
        var passedScores = dailyStatistics.Scores.Count;
        var beatmapsPlayed = dailyStatistics.BeatmapsPlayed.Count;

        var mostPpScores = dailyStatistics.Scores.OrderByDescending(m => m.Pp)
            .Select(score => (dailyStatistics.ActiveUsers.First(u => u.Id == score.UserId), score))
            .ToArray();

        var usersAndTheirScores = dailyStatistics.ActiveUsers.Select(m =>
            {
                return (m, dailyStatistics.Scores.Where(s => s.UserId == m.Id).ToArray());
            }).OrderByDescending(m => m.Item2.Length)
            .ToArray();

        var mostPlayedBeatmaps = dailyStatistics.Scores
            .GroupBy(m => m.BeatmapId!.Value)
            .OrderByDescending(m => m.Count())
            .ToArray();

        var topPpScores = "";
        var count = 0;
        foreach (var us in mostPpScores)
        {
            if (count >= 5) break;

            var scoreUrlInLink =
                UserHelper.GetUserProfileUrlWrappedInUsernameString(us.Item1.Id.Value,
                    us.Item1.Username!);
            var ppText =
                GetScoreUrlWrappedInString(us.score.Id!.Value, $"{GetFormattedPpTextConsideringNull(us.score.Pp)}pp");

            topPpScores +=
                $"{count + 1}. <b>{scoreUrlInLink}</b> - {ppText}\n";
            count += 1;
        }

        var topActivePlayers = "";
        count = 0;
        foreach (var us in usersAndTheirScores)
        {
            if (count >= 5) break;
            topActivePlayers +=
                $"{count + 1}. <b>{UserHelper.GetUserProfileUrlWrappedInUsernameString(us.m.Id.Value, us.m.Username!)}</b> — {us.Item2.Length} скоров, макс. <i>{us.Item2.Max(m => m.Pp):N2}pp</i>\n";
            count += 1;
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

        DateTime tashkentNow = TimeZoneInfo.ConvertTime(dailyStatistics.DayOfStatistic, TimeZoneInfo.FindSystemTimeZoneById("West Asia Standard Time"));
        var sendText = language.send_dailyStatistic.Fill([
            $"{tashkentNow:dd.MM.yyyy HH:mm} (по тшк.)",
            $"{activePlayersCount}",
            $"{passedScores}",
            $"{beatmapsPlayed}",
            $"{topPpScores}\n",
            $"{topActivePlayers}\n",
            $"{topMostPlayedBeatmaps}\n"
        ]);

        return sendText;
    }
}