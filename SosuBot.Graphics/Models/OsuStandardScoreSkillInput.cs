namespace SosuBot.Graphics.Models;

/// <summary>
/// The map difficulty and actual/SS performance components needed to estimate one
/// osu!standard skill evidence point. Performance components are produced by the
/// official osu!lazer calculator; this model deliberately contains no PP total.
/// </summary>
public sealed record OsuStandardScoreSkillInput
{
    public required int BeatmapId { get; init; }
    public string ModSignature { get; init; } = "NM";

    public required double StarRating { get; init; }
    public required double AimDifficulty { get; init; }
    public required double AimDifficultSliderCount { get; init; }
    public required double AimDifficultStrainCount { get; init; }
    public required double SpeedDifficulty { get; init; }
    public required double SpeedNoteCount { get; init; }
    public required double SpeedDifficultStrainCount { get; init; }
    public required double ReadingDifficulty { get; init; }

    public required int HitCircleCount { get; init; }
    public required int SliderCount { get; init; }
    public required int SpinnerCount { get; init; }
    public required int TotalHitObjects { get; init; }
    public required double OverallDifficulty { get; init; }
    public required double ApproachRate { get; init; }
    public required double ClockRate { get; init; }

    public required double ActualAimPerformance { get; init; }
    public required double ActualSpeedPerformance { get; init; }
    public required double ActualAccuracyPerformance { get; init; }
    public required double PerfectAimPerformance { get; init; }
    public required double PerfectSpeedPerformance { get; init; }
    public required double PerfectAccuracyPerformance { get; init; }
}
