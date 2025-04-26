using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;
using OsuApi.Core.V2.Scores;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace SosuBot.Extensions
{
    public static class OsuTypesExtensions
    {
        private static Mod[] AllOsuMods = new Mod[]{
            new OsuModAccuracyChallenge(),
            new OsuModAlternate(),
            new OsuModApproachDifferent(),
            new OsuModAutopilot(),
            new OsuModAutoplay(),
            new OsuModBarrelRoll(),
            new OsuModBlinds(),
            new OsuModBloom(),
            new OsuModBubbles(),
            new OsuModCinema(),
            new OsuModClassic(),
            new OsuModDaycore(),
            new OsuModDeflate(),
            new OsuModDepth(),
            new OsuModDifficultyAdjust(),
            new OsuModDoubleTime(),
            new OsuModEasy(),
            new OsuModFlashlight(),
            new OsuModFreezeFrame(),
            new OsuModHalfTime(),
            new OsuModHardRock(),
            new OsuModHidden(),
            new OsuModMirror(),
            new OsuModMuted(),
            new OsuModNightcore(),
            new OsuModNoFail(),
            new OsuModNoScope(),
            new OsuModPerfect(),
            new OsuModRandom(),
            new OsuModRelax(),
            new OsuModSingleTap(),
            new OsuModSpinIn(),
            new OsuModSpunOut(),
            new OsuModStrictTracking(),
            new OsuModSuddenDeath(),
            new OsuModSynesthesia(),
            new OsuModTargetPractice(),
            new OsuModTouchDevice(),
            new OsuModTraceable(),
        };
        public static Mod[] ToOsuMods(this OsuApi.Core.V2.Scores.Models.Mod[] mods)
        {
            List<Mod> osuMods = new List<Mod>();
            foreach (var mod in mods)
            {
                Mod? osuMod = AllOsuMods.FirstOrDefault(m => m.Acronym == mod.Acronym);
                if (osuMod is not null) osuMods.Add(osuMod);
            }
            return osuMods.ToArray();
        }

        public static double CalculateCompletion(this OsuApi.Core.V2.Scores.Models.Score score, int beatmapObjects)
        {
            int scoreHittedObjects = score.CalculateObjectsAmount();
            return (scoreHittedObjects / (double)beatmapObjects) * 100.0;
        }

        public static int CalculateObjectsAmount(this OsuApi.Core.V2.Scores.Models.Score score)
        {
            return score.Statistics!.Great + score.Statistics.Ok + score.Statistics.Meh + score.Statistics.Miss;
        }

        public static int CalculateObjectsAmount(this OsuApi.Core.V2.Users.Models.BeatmapExtended beatmapExtended)
        {
            return beatmapExtended.CountCircles!.Value + beatmapExtended.CountSliders!.Value + beatmapExtended.CountSpinners!.Value;
        }
    }
}
