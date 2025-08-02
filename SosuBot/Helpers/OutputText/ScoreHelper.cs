using Microsoft.Extensions.Logging;
using osu.Game.Rulesets.Mods;
using OsuApi.V2;
using OsuApi.V2.Clients.Beatmaps.HttpIO;
using OsuApi.V2.Models;
using OsuApi.V2.Users.Models;
using SosuBot.Extensions;
using SosuBot.Helpers.OutputText;
using SosuBot.Helpers.Types;
using SosuBot.Helpers.Types.Statistics;
using SosuBot.Localization;
using SosuBot.Localization.Languages;
using SosuBot.Services.BackgroundServices;
using Mod = OsuApi.V2.Models.Mod;

namespace SosuBot.Helpers.Scoring
{
    public static class ScoreHelper
    {
        public static string GetModsText(Mod[] mods)
        {
            string modsText = "+" + string.Join("", mods!.Select(m =>
            {
                string speedChangeString = "";
                if (m.Settings?.SpeedChange.HasValue ?? false)
                {
                    speedChangeString = $"({m.Settings.SpeedChange:0.0}x)";
                }
                return m.Acronym + speedChangeString;
            }));
            if (modsText == "+") modsText += "NM";
            return modsText;
        }

        public static string GetScorePPText(double? scorePP, string format = "N2")
        {
            string ppText = scorePP?.ToString(format) ?? "—";
            return ppText;
        }

        public static string GetScoreStatisticsText(ScoreStatistics scoreStatistics, Playmode playmode)
        {
            string scoreStatisticsText = string.Empty;
            switch (playmode)
            {
                case Playmode.Osu:
                case Playmode.Taiko:
                    scoreStatisticsText +=
                        $"{scoreStatistics.Great}x300 / {scoreStatistics.Ok}x100 / {scoreStatistics.Meh}x50";
                    break;
                case Playmode.Catch:
                    scoreStatisticsText +=
                        $"{scoreStatistics.Great}x300 / {scoreStatistics.LargeTickHit}x100 / {scoreStatistics.SmallTickHit}x50 / {scoreStatistics.SmallTickMiss}xKatu";
                    break;
                case Playmode.Mania:
                    scoreStatisticsText +=
                        $"{scoreStatistics.Perfect}x320 / {scoreStatistics.Great}x300 / {scoreStatistics.Good}x200 / {scoreStatistics.Ok}x100 / {scoreStatistics.Meh}x50";
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
            int activePlayersCount = dailyStatistics.ActiveUsers.Count;
            int passedScores = dailyStatistics.Scores.Count;
            int beatmapsPlayed = dailyStatistics.BeatmapsPlayed.Count;

            var mostPPScores = dailyStatistics.Scores.OrderByDescending(m => m.Pp)
                .Select(score => (dailyStatistics.ActiveUsers.First(u => u.Id == score.UserId), score))
                .ToArray();

            var usersAndTheirScores = dailyStatistics.ActiveUsers.Select(m =>
            {
                return (m, dailyStatistics.Scores.Where(s => s.UserId == m.Id).ToArray());
            }) .OrderByDescending(m => m.Item2.Length)
                .ToArray();
            
            var mostPlayedBeatmaps = dailyStatistics.Scores
                .GroupBy(m => m.BeatmapId!.Value)
                .OrderByDescending(m => m.Count())
                .ToArray();

            string topPPScores = "";
            int count = 0;
            foreach (var us in mostPPScores)
            {
                if (count >= 5) break;
                
                string scoreUrlInLink =
                    UserHelper.GetUserProfileUrlWrappedInUsernameString(us.Item1.Id.Value,
                        us.Item1.Username!);
                string ppText =
                    ScoreHelper.GetScoreUrlWrappedInString(us.score.Id!.Value, $"{us.score.Pp:N2}pp");
                
                topPPScores +=
                    $"{count + 1}. <b>{scoreUrlInLink}</b> от {ppText}\n";
                count += 1;
            }
            
            string topActivePlayers = "";
            count = 0;
            foreach (var us in usersAndTheirScores)
            {
                if (count >= 5) break;
                topActivePlayers +=
                    $"{count + 1}. <b>{UserHelper.GetUserProfileUrlWrappedInUsernameString(us.m.Id.Value, us.m.Username!)}</b> — {us.Item2.Length} скоров, макс. <i>{us.Item2.Max(m => m.Pp):N2}pp</i>\n";
                count += 1;
            }

            string topMostPlayedBeatmaps = "";
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
                    dailyStatistics.CachedBeatmapsetsFromOsuApi[us.Key] = beatmapsetExtended!;
                }

                topMostPlayedBeatmaps +=
                    $"{count + 1}. (<b>{beatmap!.DifficultyRating}⭐️</b>) <a href=\"https://osu.ppy.sh/beatmaps/{beatmap.Id}\">{beatmapsetExtended.Title.EncodeHtml()} [{beatmap.Version.EncodeHtml()}]</a> — <b>{us.Count()} скоров</b>\n";
                count += 1;
            }

            string sendText = language.send_dailyStatistic.Fill([
                $"{dailyStatistics.DayOfStatistic:dd.MM.yyyy HH:mm}",
                $"{activePlayersCount}",
                $"{passedScores}",
                $"{beatmapsPlayed}",
                $"{topPPScores}\n",
                $"{topActivePlayers}\n",
                $"{topMostPlayedBeatmaps}\n",
            ]);

            return sendText;
        }
    }
}