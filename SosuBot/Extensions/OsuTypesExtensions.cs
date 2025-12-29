using osu.Game.Rulesets.Catch.Mods;
using osu.Game.Rulesets.Mania.Mods;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.Taiko.Mods;
using OsuApi.V2.Models;
using OsuApi.V2.Users.Models;
using SosuBot.Database.Models;
using SosuBot.Helpers;
using Mod = osu.Game.Rulesets.Mods.Mod;

namespace SosuBot.Extensions;

public static class OsuTypesExtensions
{
    public static readonly Mod[] AllOsuMods = typeof(OsuModNoFail).Assembly.GetTypes()
        .Where(t => t.IsClass && !t.IsAbstract && t.IsPublic)
        .Where(t => typeof(Mod).IsAssignableFrom(t))
        .Where(t => t.Name.StartsWith("OsuMod"))
        .Select(t => (Mod)Activator.CreateInstance(t)!)
        .ToArray();

    public static readonly Mod[] AllManiaMods = typeof(ManiaModNoFail).Assembly.GetTypes()
        .Where(t => t.IsClass && !t.IsAbstract && t.IsPublic)
        .Where(t => typeof(Mod).IsAssignableFrom(t))
        .Where(t => t.Name.StartsWith("ManiaMod"))
        .Select(t => (Mod)Activator.CreateInstance(t)!)
        .ToArray();

    public static readonly Mod[] AllTaikoMods = typeof(TaikoModNoFail).Assembly.GetTypes()
        .Where(t => t.IsClass && !t.IsAbstract && t.IsPublic)
        .Where(t => typeof(Mod).IsAssignableFrom(t))
        .Where(t => t.Name.StartsWith("TaikoMod"))
        .Select(t => (Mod)Activator.CreateInstance(t)!)
        .ToArray();

    public static readonly Mod[] AllCatchMods = typeof(CatchModNoFail).Assembly.GetTypes()
        .Where(t => t.IsClass && !t.IsAbstract && t.IsPublic)
        .Where(t => typeof(Mod).IsAssignableFrom(t))
        .Where(t => t.Name.StartsWith("CatchMod"))
        .Select(t => (Mod)Activator.CreateInstance(t)!)
        .ToArray();

    public static readonly Mod[] AllMods = typeof(ModNoFail).Assembly.GetTypes()
        .Where(t => t.IsClass && !t.IsAbstract && t.IsPublic)
        .Where(t => typeof(Mod).IsAssignableFrom(t))
        .Where(t => t.Name.StartsWith("Mod"))
        .Select(t => (Mod)Activator.CreateInstance(t)!)
        .ToArray();

    public static Mod[] ToOsuMods(this OsuApi.V2.Models.Mod[] mods, Playmode playmode)
    {
        var osuMods = new List<Mod>();
        foreach (var mod in mods)
        {
            var rulesetMods = playmode switch
            {
                Playmode.Osu => AllOsuMods,
                Playmode.Taiko => AllTaikoMods,
                Playmode.Mania => AllManiaMods,
                Playmode.Catch => AllCatchMods,
                _ => throw new NotImplementedException()
            };
            var foundMod =
                rulesetMods.FirstOrDefault(m =>
                    m.Acronym.Equals(mod.Acronym, StringComparison.InvariantCultureIgnoreCase));

            if (foundMod == null)
            {
                foundMod = AllMods.FirstOrDefault(m =>
                    m.Acronym.Equals(mod.Acronym, StringComparison.InvariantCultureIgnoreCase));
            }

            if (foundMod == null)
            {
                osuMods.Add(new ModIdk());
                continue;
            }

            var modType = foundMod.GetType();
            var osuMod = Activator.CreateInstance(modType) as Mod;
            if (osuMod is ModRateAdjust rateAdjustMod && mod.Settings?.SpeedChange != null)
            {
                rateAdjustMod.SpeedChange.Value = mod.Settings.SpeedChange.Value;
                osuMod = rateAdjustMod;
            }
            else if (osuMod is IHasSeed seedMode && mod.Settings?.Seed != null)
            {
                seedMode.Seed.Value = mod.Settings.Seed.Value;
            }

            osuMods.Add(osuMod!);
        }

        return osuMods.ToArray();
    }

    public static string ModsToString(this Mod[] mods, Playmode playmode,
        bool acronymsToUpper = true)
    {
        if (mods.Length == 0) return "NM";
        return string.Join("",
            mods.Select(m => acronymsToUpper ? m.Acronym.ToUpperInvariant() : m.Acronym.ToLowerInvariant()));
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
            Playmode.Osu => Ruleset.Osu,
            Playmode.Taiko => Ruleset.Taiko,
            Playmode.Catch => Ruleset.Fruits,
            Playmode.Mania => Ruleset.Mania,
            _ => throw new NotImplementedException()
        };
    }

    public static int CalculateSumOfHitResults(this Score score, Playmode playmode)
    {
        int sum = score.Statistics!.Perfect
               + score.Statistics.Great
               + score.Statistics.Good
               + score.Statistics.Ok
               + score.Statistics.Meh
               + score.Statistics.Miss;
        return sum;
    }
    public static int CalculateObjectsAmount(this BeatmapExtended beatmapExtended)
    {
        return beatmapExtended.CountCircles!.Value + beatmapExtended.CountSliders!.Value +
               beatmapExtended.CountSpinners!.Value;
    }

    public static Dictionary<HitResult, int> GetMaximumStatistics(
        this BeatmapExtended beatmapExtended, Playmode playmode = Playmode.Osu)
    {
        return playmode switch
        {
            Playmode.Osu => new Dictionary<HitResult, int>
            {
                {
                    HitResult.Great,
                    beatmapExtended.CountCircles!.Value + beatmapExtended.CountSliders!.Value +
                    beatmapExtended.CountSpinners!.Value
                }
            },
            Playmode.Mania => new Dictionary<HitResult, int>
            {
                {
                    HitResult.Perfect,
                    beatmapExtended.CountCircles!.Value + 2 * beatmapExtended.CountSliders!.Value +
                    beatmapExtended.CountSpinners!.Value
                }
            },
            Playmode.Catch => throw new NotImplementedException(),
            Playmode.Taiko => throw new NotImplementedException(),
            _ => throw new NotImplementedException()
        };
    }
}