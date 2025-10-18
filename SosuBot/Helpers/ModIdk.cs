using osu.Framework.Localisation;
using osu.Game.Rulesets.Mods;

namespace SosuBot.Helpers;

public class ModIdk : Mod
{
    public override string Name { get; } = "Idk";
    public override LocalisableString Description { get; } = "Is used when the origin mode was not found";
    public override double ScoreMultiplier { get; } = 1.0;
    public override string Acronym { get; } = "**";
}