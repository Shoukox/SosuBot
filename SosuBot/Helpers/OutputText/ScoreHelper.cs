﻿using Microsoft.Extensions.Logging;
using osu.Game.Rulesets.Mods;
using OsuApi.V2;
using OsuApi.V2.Clients.Beatmaps.HttpIO;
using OsuApi.V2.Models;
using OsuApi.V2.Users.Models;
using SosuBot.Extensions;
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

        public static async Task<string> GetDailyStatisticsSendText(DailyStatistics dailyStatistics, ApiV2 osuApi,
            ILogger logger)
        {
            ILocalization language = new Russian();
            int activePlayersCount = dailyStatistics.ActiveUsers.Count;
            int passedScores = dailyStatistics.Scores.Count;
            int beatmapsPlayed = dailyStatistics.BeatmapsPlayed.Count;

            Score? mostPPForScore = dailyStatistics.Scores.MaxBy(m => m.Pp);
            User? userHavingMostPPForScore =
                dailyStatistics.ActiveUsers.FirstOrDefault(m => m.Id == mostPPForScore?.UserId);

            var usersAndTheirScores = dailyStatistics.ActiveUsers.Select(m =>
            {
                return (m, dailyStatistics.Scores.Where(s => s.UserId == m.Id).ToArray());
            }).OrderByDescending(m => { return m.Item2.Length; }).ToArray();

            
            var mostPlayedBeatmaps = dailyStatistics.Scores
                .GroupBy(m => m.BeatmapId!.Value)
                .OrderByDescending(m => m.Count()).ToArray();

            string top3ActivePlayers = "";
            int count = 0;
            foreach (var us in usersAndTheirScores)
            {
                if (count >= 5) break;
                top3ActivePlayers +=
                    $"{count + 1}. <b>{us.m.Username}</b> — {us.Item2.Length} скоров, макс. <i>{us.Item2.Max(m => m.Pp):N2}pp</i>\n";
                count += 1;
            }

            string top3MostPlayedBeatmaps = "";
            count = 0;
            foreach (var us in mostPlayedBeatmaps)
            {
                if (count >= 5) break;

                GetBeatmapResponse? beatmap = await osuApi.Beatmaps.GetBeatmap(us.Key);
                if (beatmap == null)
                {
                    logger.LogError($"Beatmap \"{us.Key}\" not found");
                    continue;
                }

                BeatmapsetExtended beatmapsetExtended =
                    await osuApi.Beatmapsets.GetBeatmapset(beatmap.BeatmapExtended!.BeatmapsetId.Value);

                top3MostPlayedBeatmaps +=
                    $"{count + 1}. (<b>{beatmap.BeatmapExtended!.DifficultyRating}⭐️</b>) <a href=\"https://osu.ppy.sh/beatmaps/{beatmap.BeatmapExtended.Id}\">{beatmapsetExtended.Title.EncodeHtml()} [{beatmap.BeatmapExtended.Version.EncodeHtml()}]</a> — <b>{us.Count()} траев</b>\n";
                count += 1;
            }

            string sendText = language.send_dailyStatistic.Fill([
                $"{dailyStatistics.DayOfStatistic:dd.MM.yyyy HH:mm}",
                $"{activePlayersCount}",
                $"{passedScores}",
                $"{beatmapsPlayed}",
                $"{ScoresObserverBackgroundService.BaseOsuScoreLink}{mostPPForScore?.Id}",
                $"{mostPPForScore?.Pp:N2}",
                $"{userHavingMostPPForScore?.GetProfileUrl()}",
                $"{userHavingMostPPForScore?.Username}",

                $"{top3ActivePlayers}\n",
                $"{top3MostPlayedBeatmaps}\n",
            ]);

            return sendText;
        }
    }
}