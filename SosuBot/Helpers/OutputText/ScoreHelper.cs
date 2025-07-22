using OsuApi.V2.Models;
using SosuBot.Helpers.OsuTypes;

namespace SosuBot.Helpers.Scoring
{
    public static class ScoreHelper
    {
        public static string GetModsText(Mod[] mods)
        {
            string modsText = "+" + string.Join("", mods!.Select(m => m.Acronym));
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
                    scoreStatisticsText += $"{scoreStatistics.Great}x300 / {scoreStatistics.Ok}x100 / {scoreStatistics.Meh}x50";
                    break;
                case Playmode.Catch:
                    scoreStatisticsText += $"{scoreStatistics.Great}x300 / {scoreStatistics.LargeTickHit}x100 / {scoreStatistics.SmallTickHit}x50 / {scoreStatistics.SmallTickMiss}xKatu";
                    break;
                case Playmode.Mania:
                    scoreStatisticsText += $"{scoreStatistics.Perfect}x320 / {scoreStatistics.Great}x300 / {scoreStatistics.Good}x200 / {scoreStatistics.Ok}x100 / {scoreStatistics.Meh}x50";
                    break;
            }
            return scoreStatisticsText;
        }
    }
}
