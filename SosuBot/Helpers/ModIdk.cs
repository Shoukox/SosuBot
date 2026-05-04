using osu.Framework.Localisation;
using osu.Game.Rulesets.Mods;

namespace SosuBot.Helpers;

public class ModIdk : Mod
{
    public override string Name { get; } = "Idk";
    public override LocalisableString Description { get; } = "This mod is used when an actual mode was not found";
    public override double ScoreMultiplier { get; } = 1.0;
    public override string Acronym { get; } = "**";
}