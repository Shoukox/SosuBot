namespace SosuBot.Graphics.Models;

public sealed record PlayerScoreSkillInput
{
    public required OsuGameMode Mode { get; init; }
    public required double StarRating { get; init; }
    public required double AccuracyPercent { get; init; }
    public required double Bpm { get; init; }
    public required double CircleSize { get; init; }
    public required double ApproachRate { get; init; }
    public required double OverallDifficulty { get; init; }
    public required double DrainRate { get; init; }
    public required int Combo { get; init; }
    public required int MaximumCombo { get; init; }
    public required int HitCircleCount { get; init; }
    public required int SliderCount { get; init; }
    public required double AimDifficulty { get; init; }
    public required double SpeedDifficulty { get; init; }
    public required double SpeedNoteCount { get; init; }
    public required string[] Mods { get; init; }
}
