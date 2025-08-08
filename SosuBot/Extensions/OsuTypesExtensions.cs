using osu.Game.Rulesets.Catch.Mods;
using osu.Game.Rulesets.Mania.Mods;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.Taiko.Mods;
using OsuApi.V2.Models;
using OsuApi.V2.Users.Models;
using SosuBot.Helpers.Types;
using Mod = osu.Game.Rulesets.Mods.Mod;

namespace SosuBot.Extensions;

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
        var osuMods = new List<Mod>();
        foreach (var mod in mods)
        {
            var osuMod = playmode switch
            {
                Playmode.Osu => AllOsuMods.FirstOrDefault(m => m.Acronym == mod.Acronym),
                Playmode.Taiko => AllTaikoMods.FirstOrDefault(m => m.Acronym == mod.Acronym),
                Playmode.Mania => AllManiaMods.FirstOrDefault(m => m.Acronym == mod.Acronym),
                Playmode.Catch => AllCatchMods.FirstOrDefault(m => m.Acronym == mod.Acronym),
                _ => throw new NotImplementedException()
            };
            if (osuMod is ModDoubleTime dtMode && mod.Settings?.SpeedChange != null)
            {
                dtMode.SpeedChange.Value = mod.Settings.SpeedChange.Value;
                osuMod = dtMode;
            }
            else if (osuMod is ModRandom rdMode && mod.Settings?.Seed != null)
            {
                rdMode.Seed.Value = mod.Settings.Seed.Value;
                osuMod = rdMode;
            }

            if (osuMod is not null)
            {
                osuMods.Add(osuMod);
            }
        }

        return osuMods.ToArray();
    }

    public static string ModsToString(this Mod[] mods, Playmode playmode,
        bool acronymsToUpper = true)
    {
        if (mods == null || mods.Length == 0) return "NM";
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
            Playmode.Osu => Ruleset.Osu,
            Playmode.Taiko => Ruleset.Taiko,
            Playmode.Catch => Ruleset.Fruits,
            Playmode.Mania => Ruleset.Mania,
            _ => throw new NotImplementedException()
        };
    }

    public static double CalculateCompletion(this Score score, int beatmapObjects)
    {
        var scoreHittedObjects = score.CalculateObjectsAmount();
        return scoreHittedObjects / (double)beatmapObjects * 100.0;
    }

    public static int CalculateObjectsAmount(this Score score)
    {
        return score.Statistics!.Great + score.Statistics.Ok + score.Statistics.Meh + score.Statistics.Miss;
    }

    public static int CalculateObjectsAmount(this BeatmapExtended beatmapExtended)
    {
        return beatmapExtended.CountCircles!.Value + beatmapExtended.CountSliders!.Value +
               beatmapExtended.CountSpinners!.Value;
    }

    public static Dictionary<HitResult, int> GetMaximumStatistics(
        this BeatmapExtended beatmapExtended)
    {
        return new Dictionary<HitResult, int>
        {
            {
                HitResult.Great,
                beatmapExtended.CountCircles!.Value + beatmapExtended.CountSliders!.Value +
                beatmapExtended.CountSpinners!.Value
            }
        };
    }
}