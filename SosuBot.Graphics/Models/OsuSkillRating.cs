namespace SosuBot.Graphics.Models;

/// <summary>
/// Capability estimates derived from the strongest osu!standard evidence in a
/// player's best scores. Confidence describes evidence coverage, not ability.
/// </summary>
public sealed record OsuSkillRating(
    double Aim,
    double Speed,
    double Accuracy,
    double AimConfidence,
    double SpeedConfidence,
    double AccuracyConfidence,
    double RawAim,
    double RawSpeed,
    double RawAccuracy,
    double AverageStars,
    int CalculatedScores,
    IReadOnlyList<ScoreSkillEvidence> Evidence)
{
    public PlayerSkills ToPlayerSkills() => new(
        Aim,
        Speed,
        Accuracy,
        AverageStars,
        CalculatedScores);
}
