using osu.Game.Rulesets.Scoring;
using OsuApi.Core.V2.Scores.Models;

namespace SosuBot.Extensions
{
    public static class StatisticsExtensions
    {
        public static Dictionary<HitResult, int> ToStatistics(this ScoreStatistics statistics)
        {
            var result = new Dictionary<HitResult, int>();
            result.TryAdd(HitResult.Miss, statistics.Miss);
            result.TryAdd(HitResult.Meh, statistics.Meh);
            result.TryAdd(HitResult.Ok, statistics.Ok);
            result.TryAdd(HitResult.Good, statistics.Good);
            result.TryAdd(HitResult.Great, statistics.Great);
            result.TryAdd(HitResult.Perfect, statistics.Perfect);
            result.TryAdd(HitResult.SmallTickMiss, statistics.SmallTickMiss);
            result.TryAdd(HitResult.SmallTickHit, statistics.SmallTickHit);
            result.TryAdd(HitResult.LargeTickMiss, statistics.LargeTickMiss);
            result.TryAdd(HitResult.LargeTickHit, statistics.LargeTickHit);
            result.TryAdd(HitResult.SmallBonus, statistics.SmallBonus);
            result.TryAdd(HitResult.LargeBonus, statistics.LargeBonus);
            result.TryAdd(HitResult.IgnoreMiss, statistics.IgnoreMiss);
            result.TryAdd(HitResult.IgnoreHit, statistics.IgnoreHit);
            //result.TryAdd(HitResult.ComboBreak, statistics.ComboBreak);
            result.TryAdd(HitResult.SliderTailHit, statistics.SliderTailHit);
#pragma warning disable CS0618 // Type or member is obsolete
            result.TryAdd(HitResult.LegacyComboIncrease, statistics.LegacyComboIncrease);
#pragma warning restore CS0618 // Type or member is obsolete
            return result;
        }

        public static int GetMaxCombo(this ScoreStatistics statistics)
        {
            return statistics.Perfect + statistics.Great + statistics.Good + statistics.Ok + statistics.Meh + statistics.Miss + statistics.LegacyComboIncrease;
        }
    }
}
