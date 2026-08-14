namespace SosuBot.Graphics.Models;

/// <summary>
/// Explainable contribution of one score to the three standard skill estimates.
/// </summary>
public sealed record ScoreSkillEvidence(
    int BeatmapId,
    string ModSignature,
    double AimChallenge,
    double SpeedChallenge,
    double AccuracyChallenge,
    double AimRelevance,
    double SpeedRelevance,
    double AccuracyRelevance,
    double AimExecutionQuality,
    double SpeedExecutionQuality,
    double AccuracyExecutionQuality,
    double AimEvidence,
    double SpeedEvidence,
    double AccuracyEvidence,
    double ActualAimPerformance,
    double ActualSpeedPerformance,
    double ActualAccuracyPerformance,
    double PerfectAimPerformance,
    double PerfectSpeedPerformance,
    double PerfectAccuracyPerformance);
