namespace SosuBot.Graphics;

/// <summary>
/// Tunable parameters for the osu!standard capability model.
///
/// ExecutionExponent softens the effect of misses/chokes already represented by
/// lazer's component performance. RelevanceExponent separates aim-heavy and
/// speed-heavy maps. AggregationDecay controls how quickly lower evidence points
/// lose influence. The remaining values define the independent display scale.
/// </summary>
public sealed class SkillRatingOptions
{
    /// <summary>Exponent for actual/perfect execution. Values below one soften choke penalties.</summary>
    public double ExecutionExponent { get; init; } = 0.35;

    /// <summary>Exponent for map relevance. Larger values suppress off-specialisation maps more strongly.</summary>
    public double RelevanceExponent { get; init; } = 0.75;

    /// <summary>Minimum relevance required for a score to enter a skill's evidence set.</summary>
    public double MinimumRelevance { get; init; } = 0.35;

    /// <summary>Number of strongest evidence points retained for each independent skill.</summary>
    public int ScoresUsed { get; init; } = 10;

    /// <summary>Rank-to-rank weight multiplier for evidence below the strongest score.</summary>
    public double AggregationDecay { get; init; } = 0.82;

    /// <summary>Relevant-evidence mass needed for confidence 1.0.</summary>
    public double ConfidenceEvidenceCount { get; init; } = 5;

    /// <summary>Displayed rating at <see cref="RatingBaselineSkill"/> raw skill.</summary>
    public double RatingScale { get; init; } = 650;

    /// <summary>Neutral raw skill used when a skill has no relevant evidence.</summary>
    public double RatingBaselineSkill { get; init; } = 6;

    /// <summary>Exponent of the monotonic raw-skill-to-display transform.</summary>
    public double RatingExponent { get; init; } = 2;

    /// <summary>Base raw challenge assigned before the transformed perfect-accuracy component.</summary>
    public double AccuracyChallengeOffset { get; init; } = 2.5;

    /// <summary>Multiplier converting the transformed perfect-accuracy component into challenge.</summary>
    public double AccuracyChallengeScale { get; init; } = 3.3;

    /// <summary>Reference value in the units of lazer's Accuracy performance component.</summary>
    public double AccuracyPerformanceReference { get; init; } = 100;

    /// <summary>Exponent used to compress the spread of lazer's perfect Accuracy component.</summary>
    public double AccuracyChallengeExponent { get; init; } = 1.0 / 3.0;

    public void Validate()
    {
        if (!double.IsFinite(ExecutionExponent) || ExecutionExponent <= 0)
            throw new ArgumentOutOfRangeException(nameof(ExecutionExponent));
        if (!double.IsFinite(RelevanceExponent) || RelevanceExponent <= 0)
            throw new ArgumentOutOfRangeException(nameof(RelevanceExponent));
        if (!double.IsFinite(MinimumRelevance) || MinimumRelevance < 0 || MinimumRelevance > 1)
            throw new ArgumentOutOfRangeException(nameof(MinimumRelevance));
        if (ScoresUsed <= 0)
            throw new ArgumentOutOfRangeException(nameof(ScoresUsed));
        if (!double.IsFinite(AggregationDecay) || AggregationDecay <= 0 || AggregationDecay > 1)
            throw new ArgumentOutOfRangeException(nameof(AggregationDecay));
        if (!double.IsFinite(ConfidenceEvidenceCount) || ConfidenceEvidenceCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(ConfidenceEvidenceCount));
        if (!double.IsFinite(RatingScale) || RatingScale <= 0)
            throw new ArgumentOutOfRangeException(nameof(RatingScale));
        if (!double.IsFinite(RatingBaselineSkill) || RatingBaselineSkill <= 0)
            throw new ArgumentOutOfRangeException(nameof(RatingBaselineSkill));
        if (!double.IsFinite(RatingExponent) || RatingExponent <= 0)
            throw new ArgumentOutOfRangeException(nameof(RatingExponent));
        if (!double.IsFinite(AccuracyChallengeOffset) || AccuracyChallengeOffset < 0)
            throw new ArgumentOutOfRangeException(nameof(AccuracyChallengeOffset));
        if (!double.IsFinite(AccuracyChallengeScale) || AccuracyChallengeScale <= 0)
            throw new ArgumentOutOfRangeException(nameof(AccuracyChallengeScale));
        if (!double.IsFinite(AccuracyPerformanceReference) || AccuracyPerformanceReference <= 0)
            throw new ArgumentOutOfRangeException(nameof(AccuracyPerformanceReference));
        if (!double.IsFinite(AccuracyChallengeExponent) || AccuracyChallengeExponent <= 0)
            throw new ArgumentOutOfRangeException(nameof(AccuracyChallengeExponent));
    }
}
