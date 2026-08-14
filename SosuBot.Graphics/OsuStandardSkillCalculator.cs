using SosuBot.Graphics.Models;

namespace SosuBot.Graphics;

/// <summary>
/// Estimates osu!standard capability from difficulty attributes and actual versus
/// hypothetical-SS osu!lazer component performance.
/// </summary>
public sealed class OsuStandardSkillCalculator
{
    public const int MaximumScoreCount = 50;

    private readonly SkillRatingOptions _options;

    public OsuStandardSkillCalculator(SkillRatingOptions? options = null)
    {
        _options = options ?? new SkillRatingOptions();
        _options.Validate();
    }

    public OsuSkillRating Calculate(IEnumerable<OsuStandardScoreSkillInput?> scores)
    {
        ArgumentNullException.ThrowIfNull(scores);

        OsuStandardScoreSkillInput[] inputs = scores
            .Take(MaximumScoreCount)
            .Where(input => input is not null)
            .Select(input => input!)
            .Where(IsValid)
            .ToArray();

        List<ScoreSkillEvidence> evidence = inputs
            .Select(CreateEvidence)
            .Where(item => item is not null)
            .Select(item => item!)
            .ToList();

        SkillAggregate aim = Aggregate(evidence, static item => item.AimEvidence,
            static item => item.AimRelevance, static item => item.AimChallenge);
        SkillAggregate speed = Aggregate(evidence, static item => item.SpeedEvidence,
            static item => item.SpeedRelevance, static item => item.SpeedChallenge);
        SkillAggregate accuracy = Aggregate(evidence, static item => item.AccuracyEvidence,
            static item => item.AccuracyRelevance, static item => item.AccuracyChallenge);

        double averageStars = inputs.Length == 0
            ? 0
            : inputs.Average(input => input.StarRating);

        return new OsuSkillRating(
            Aim: Scale(aim.RawSkill),
            Speed: Scale(speed.RawSkill),
            Accuracy: Scale(accuracy.RawSkill),
            AimConfidence: aim.Confidence,
            SpeedConfidence: speed.Confidence,
            AccuracyConfidence: accuracy.Confidence,
            RawAim: aim.RawSkill,
            RawSpeed: speed.RawSkill,
            RawAccuracy: accuracy.RawSkill,
            AverageStars: averageStars,
            CalculatedScores: inputs.Length,
            Evidence: evidence);
    }

    private ScoreSkillEvidence? CreateEvidence(OsuStandardScoreSkillInput input)
    {
        double aimDifficulty = PositiveFinite(input.AimDifficulty);
        double speedDifficulty = PositiveFinite(input.SpeedDifficulty);
        double perfectAccuracyPerformance = PositiveFinite(input.PerfectAccuracyPerformance);

        double aimLengthFactor = LengthFactor(
            Math.Max(input.AimDifficultStrainCount + input.AimDifficultSliderCount,
                input.TotalHitObjects));
        double speedLengthFactor = LengthFactor(
            Math.Max(input.SpeedNoteCount, Math.Max(input.SpeedDifficultStrainCount, input.TotalHitObjects)));
        double accuracyLengthFactor = LengthFactor(input.HitCircleCount + input.SliderCount);

        double aimChallenge = aimDifficulty * aimLengthFactor;
        double speedChallenge = speedDifficulty * speedLengthFactor;
        double accuracyChallenge = perfectAccuracyPerformance <= 0
            ? 0
            : _options.AccuracyChallengeOffset
              + _options.AccuracyChallengeScale
              * Math.Pow(perfectAccuracyPerformance / _options.AccuracyPerformanceReference,
                  _options.AccuracyChallengeExponent);

        double aimRelevance = DirectionalRelevance(aimDifficulty, speedDifficulty);
        double speedRelevance = DirectionalRelevance(speedDifficulty, aimDifficulty);
        double accuracyRelevance = perfectAccuracyPerformance > 0
            ? Math.Clamp(0.75 + 0.25 * accuracyLengthFactor, 0, 1)
            : 0;

        double aimExecution = ExecutionQuality(input.ActualAimPerformance, input.PerfectAimPerformance);
        double speedExecution = ExecutionQuality(input.ActualSpeedPerformance, input.PerfectSpeedPerformance);
        double accuracyExecution = ExecutionQuality(input.ActualAccuracyPerformance, input.PerfectAccuracyPerformance);

        return new ScoreSkillEvidence(
            input.BeatmapId,
            input.ModSignature,
            aimChallenge,
            speedChallenge,
            accuracyChallenge,
            aimRelevance,
            speedRelevance,
            accuracyRelevance,
            aimExecution,
            speedExecution,
            accuracyExecution,
            EvidenceValue(aimChallenge, aimRelevance, aimExecution),
            EvidenceValue(speedChallenge, speedRelevance, speedExecution),
            EvidenceValue(accuracyChallenge, accuracyRelevance, accuracyExecution),
            input.ActualAimPerformance,
            input.ActualSpeedPerformance,
            input.ActualAccuracyPerformance,
            input.PerfectAimPerformance,
            input.PerfectSpeedPerformance,
            input.PerfectAccuracyPerformance);
    }

    private SkillAggregate Aggregate(
        IEnumerable<ScoreSkillEvidence> allEvidence,
        Func<ScoreSkillEvidence, double> evidenceSelector,
        Func<ScoreSkillEvidence, double> relevanceSelector,
        Func<ScoreSkillEvidence, double> challengeSelector)
    {
        ScoreSkillEvidence[] candidates = allEvidence
            .Where(item => challengeSelector(item) > 0
                           && relevanceSelector(item) >= _options.MinimumRelevance
                           && evidenceSelector(item) > 0)
            .OrderByDescending(evidenceSelector)
            .Take(_options.ScoresUsed)
            .ToArray();

        if (candidates.Length == 0)
        {
            // A missing skill-specific sample is an uncertainty, not proof of
            // zero ability. Keep a neutral prior and report low confidence.
            return new SkillAggregate(_options.RatingBaselineSkill, 0);
        }

        double weightedEvidence = 0;
        double totalWeight = 0;
        double relevanceMass = 0;

        for (int index = 0; index < candidates.Length; index++)
        {
            double weight = Math.Pow(_options.AggregationDecay, index);
            weightedEvidence += evidenceSelector(candidates[index]) * weight;
            totalWeight += weight;
            relevanceMass += relevanceSelector(candidates[index]);
        }

        double rawSkill = totalWeight > 0 ? weightedEvidence / totalWeight : 0;
        double confidence = Math.Clamp(relevanceMass / _options.ConfidenceEvidenceCount, 0, 1);
        return new SkillAggregate(rawSkill, confidence);
    }

    private double Scale(double rawSkill)
    {
        if (!double.IsFinite(rawSkill) || rawSkill <= 0)
            return 0;

        return _options.RatingScale
               * Math.Pow(rawSkill / _options.RatingBaselineSkill, _options.RatingExponent);
    }

    private double EvidenceValue(double challenge, double relevance, double execution)
    {
        if (!double.IsFinite(challenge) || challenge <= 0 || relevance <= 0 || execution <= 0)
            return 0;

        return challenge
               * Math.Pow(execution, _options.ExecutionExponent)
               * Math.Pow(relevance, _options.RelevanceExponent);
    }

    private static double ExecutionQuality(double actual, double perfect)
    {
        if (!double.IsFinite(actual) || !double.IsFinite(perfect) || perfect <= 0 || actual <= 0)
            return 0;

        return Math.Clamp(actual / perfect, 0, 1);
    }

    private static double DirectionalRelevance(double component, double opposite)
    {
        if (component <= 0 || !double.IsFinite(component))
            return 0;
        if (opposite <= 0 || !double.IsFinite(opposite))
            return 1;

        // A balanced map is fully relevant to both skills. A specialist map
        // remains fully relevant to its dominant skill while suppressing the
        // opposite skill smoothly instead of applying an arbitrary mod bonus.
        return Math.Clamp(2 * component / (component + opposite), 0, 1);
    }

    private static double LengthFactor(double relevantObjectCount)
    {
        if (!double.IsFinite(relevantObjectCount) || relevantObjectCount <= 0)
            return 0;

        double progress = Math.Clamp(Math.Log(1 + relevantObjectCount) / Math.Log(1 + 1000), 0, 1);
        return 0.85 + 0.30 * progress;
    }

    private static bool IsValid(OsuStandardScoreSkillInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        return input.BeatmapId > 0
               && input.TotalHitObjects > 0
               && IsFiniteNonNegative(input.StarRating)
               && IsFiniteNonNegative(input.AimDifficulty)
               && IsFiniteNonNegative(input.AimDifficultSliderCount)
               && IsFiniteNonNegative(input.AimDifficultStrainCount)
               && IsFiniteNonNegative(input.SpeedDifficulty)
               && IsFiniteNonNegative(input.SpeedNoteCount)
               && IsFiniteNonNegative(input.SpeedDifficultStrainCount)
               && IsFiniteNonNegative(input.ReadingDifficulty)
               && input.HitCircleCount >= 0
               && input.SliderCount >= 0
               && input.SpinnerCount >= 0
               && IsFiniteNonNegative(input.OverallDifficulty)
               && IsFiniteNonNegative(input.ApproachRate)
               && IsFinitePositive(input.ClockRate)
               && IsFiniteNonNegative(input.ActualAimPerformance)
               && IsFiniteNonNegative(input.ActualSpeedPerformance)
               && IsFiniteNonNegative(input.ActualAccuracyPerformance)
               && IsFiniteNonNegative(input.PerfectAimPerformance)
               && IsFiniteNonNegative(input.PerfectSpeedPerformance)
               && IsFiniteNonNegative(input.PerfectAccuracyPerformance);
    }

    private static double PositiveFinite(double value) => IsFinitePositive(value) ? value : 0;

    private static bool IsFiniteNonNegative(double value) => double.IsFinite(value) && value >= 0;

    private static bool IsFinitePositive(double value) => double.IsFinite(value) && value > 0;

    private readonly record struct SkillAggregate(double RawSkill, double Confidence);
}
