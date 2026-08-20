namespace SosuBot.Graphics.Models;

public sealed record PlayerSkills(
    double Aim,
    double Speed,
    double Accuracy,
    double Stars,
    int CalculatedScores)
{
    public IReadOnlyList<PlayerSkillMetric> GetMetrics(OsuGameMode mode) => mode switch
    {
        OsuGameMode.Osu =>
        [
            new("Aim", Aim),
            new("Speed", Speed),
            new("Accuracy", Accuracy)
        ],
        OsuGameMode.Taiko =>
        [
            // Speed is kept as the public/API property name; for taiko it stores Stamina.
            new("Stamina", Speed),
            new("Accuracy", Accuracy)
        ],
        OsuGameMode.Catch =>
        [
            new("Aim", Aim),
            new("Accuracy", Accuracy)
        ],
        OsuGameMode.Mania =>
        [
            new("Density", Aim),
            new("Finger Control", Speed),
            new("Accuracy", Accuracy)
        ],
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown osu! mode.")
    };
}
