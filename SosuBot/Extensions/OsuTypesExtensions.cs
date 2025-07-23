using osu.Game.Rulesets.Catch.Mods;
using osu.Game.Rulesets.Mania.Mods;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.Taiko.Mods;
using SosuBot.Helpers.OsuTypes;

namespace SosuBot.Extensions
{
    public static class OsuTypesExtensions
    {
        public static readonly Mod[] AllOsuMods = typeof(OsuModNoFail).Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.IsPublic)
            .Where(t => typeof(Mod).IsAssignableFrom(t))
            .Where(t => t.Name.StartsWith("OsuMod"))
            .Select(t => (Mod)Activator.CreateInstance(t)!)
            .ToArray()!;

        public static readonly Mod[] AllManiaMods = typeof(ManiaModNoFail).Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.IsPublic)
            .Where(t => typeof(Mod).IsAssignableFrom(t))
            .Where(t => t.Name.StartsWith("ManiaMod"))
            .Select(t => (Mod)Activator.CreateInstance(t)!)
            .ToArray()!;

        public static readonly Mod[] AllTaikoMods = typeof(TaikoModNoFail).Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.IsPublic)
            .Where(t => typeof(Mod).IsAssignableFrom(t))
            .Where(t => t.Name.StartsWith("TaikoMod"))
            .Select(t => (Mod)Activator.CreateInstance(t)!)
            .ToArray()!;

        public static readonly Mod[] AllCatchMods = typeof(CatchModNoFail).Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.IsPublic)
            .Where(t => typeof(Mod).IsAssignableFrom(t))
            .Where(t => t.Name.StartsWith("CatchMod"))
            .Select(t => (Mod)Activator.CreateInstance(t)!)
            .ToArray()!;

        public static Mod[] ToOsuMods(this OsuApi.V2.Models.Mod[] mods, Playmode playmode)
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

        public static string ModsToString(this Mod[] mods, Playmode playmode,
            bool acronymsToUpper = true)
        {
            return string.Join("",
                mods.Select(m => acronymsToUpper ? m.Acronym!.ToUpperInvariant() : m.Acronym!.ToLowerInvariant()));
        }

        public static string ToGamemode(this Playmode playmode)
        {
            return playmode switch
            {
                Playmode.Osu => "osu!std",
                Playmode.Taiko => "osu!taiko",
                Playmode.Catch => "osu!catch",
                Playmode.Mania => "osu!mania",
                _ => throw new NotImplementedException()
            };
        }

        public static string ToRuleset(this Playmode playmode)
        {
            return playmode switch
            {
                Playmode.Osu => OsuApi.V2.Models.Ruleset.Osu,
                Playmode.Taiko => OsuApi.V2.Models.Ruleset.Taiko,
                Playmode.Catch => OsuApi.V2.Models.Ruleset.Fruits,
                Playmode.Mania => OsuApi.V2.Models.Ruleset.Mania,
                _ => throw new NotImplementedException()
            };
        }

        public static string GetProfileUrl(this OsuApi.V2.Users.Models.User user)
        {
            return $"http://osu.ppy.sh/u/{user.Id}";
        }

        public static double CalculateCompletion(this OsuApi.V2.Models.Score score, int beatmapObjects)
        {
            int scoreHittedObjects = score.CalculateObjectsAmount();
            return (scoreHittedObjects / (double)beatmapObjects) * 100.0;
        }

        public static int CalculateObjectsAmount(this OsuApi.V2.Models.Score score)
        {
            return score.Statistics!.Great + score.Statistics.Ok + score.Statistics.Meh + score.Statistics.Miss;
        }

        public static int CalculateObjectsAmount(this OsuApi.V2.Users.Models.BeatmapExtended beatmapExtended)
        {
            return beatmapExtended.CountCircles!.Value + beatmapExtended.CountSliders!.Value +
                   beatmapExtended.CountSpinners!.Value;
        }

        public static Dictionary<HitResult, int> GetMaximumStatistics(
            this OsuApi.V2.Users.Models.BeatmapExtended beatmapExtended)
        {
            return new Dictionary<HitResult, int>()
            {
                {
                    HitResult.Great,
                    beatmapExtended.CountCircles!.Value + beatmapExtended.CountSliders!.Value +
                    beatmapExtended.CountSpinners!.Value
                }
            };
        }
    }
}