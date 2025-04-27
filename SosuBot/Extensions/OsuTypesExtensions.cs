using osu.Game.Rulesets.Catch.Mods;
using osu.Game.Rulesets.Mania.Mods;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Taiko.Mods;
using OsuApi.Core.V2.Scores;
using SosuBot.OsuTypes;
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
        private static Mod[] AllOsuMods = typeof(OsuModNoFail).Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.IsPublic)
            .Where(t => typeof(Mod).IsAssignableFrom(t))
            .Where(t => t.Name.StartsWith("OsuMod"))
            .Select(t => (Mod)Activator.CreateInstance(t)!)
            .ToArray()!;

        private static Mod[] AllManiaMods = typeof(ManiaModNoFail).Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.IsPublic)
            .Where(t => typeof(Mod).IsAssignableFrom(t))
            .Where(t => t.Name.StartsWith("ManiaMod"))
            .Select(t => (Mod)Activator.CreateInstance(t)!)
            .ToArray()!;

        private static Mod[] AllTaikoMods = typeof(TaikoModNoFail).Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.IsPublic)
            .Where(t => typeof(Mod).IsAssignableFrom(t))
            .Where(t => t.Name.StartsWith("TaikoMod"))
            .Select(t => (Mod)Activator.CreateInstance(t)!)
            .ToArray()!;

        private static Mod[] AllCatchMods = typeof(CatchModNoFail).Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.IsPublic)
            .Where(t => typeof(Mod).IsAssignableFrom(t))
            .Where(t => t.Name.StartsWith("CatchMod"))
            .Select(t => (Mod)Activator.CreateInstance(t)!)
            .ToArray()!;

        public static Mod[] ToOsuMods(this OsuApi.Core.V2.Scores.Models.Mod[] mods, Playmode playmode)
        {
            List<Mod> osuMods = new List<Mod>();
            foreach (var mod in mods)
            {
                Mod? osuMod = playmode switch
                {
                    Playmode.Osu => AllOsuMods.FirstOrDefault(m => m.Acronym == mod.Acronym),
                    Playmode.Taiko => AllTaikoMods.FirstOrDefault(m => m.Acronym == mod.Acronym),
                    Playmode.Mania => AllManiaMods.FirstOrDefault(m => m.Acronym == mod.Acronym),
                    Playmode.Catch => AllCatchMods.FirstOrDefault(m => m.Acronym == mod.Acronym),
                    _ => throw new NotImplementedException(),
                };
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
