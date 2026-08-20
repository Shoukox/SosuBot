using SosuBot.Graphics.Models;

namespace SosuBot.Graphics;

/// <summary>
/// Ports the player skill formulas from TinyBot's calc_player_skill.js.
/// Values shown on a card are the average skill across at most 50 best scores,
/// multiplied by 100 in the same way as the original card command.
/// </summary>
public sealed class PlayerSkillCalculator
{
    public const int MaximumScoreCount = 50;

    public PlayerSkills Calculate(IEnumerable<PlayerScoreSkillInput> scores)
    {
        ArgumentNullException.ThrowIfNull(scores);

        PlayerScoreSkillInput[] inputs = scores
            .Take(MaximumScoreCount)
            .Where(IsUsable)
            .ToArray();

        if (inputs.Length == 0)
            throw new ArgumentException("At least one valid score is required to calculate player skills.",
                nameof(scores));

        double starTotal = 0;
        double aimTotal = 0;
        // The taiko calculation keeps using this legacy storage slot for Stamina.
        double speedTotal = 0;
        double accuracyTotal = 0;

        foreach (PlayerScoreSkillInput input in inputs)
        {
            starTotal += input.StarRating;

            switch (input.Mode)
            {
                case OsuGameMode.Osu:
                    CalculateOsu(input, ref aimTotal, ref speedTotal, ref accuracyTotal);
                    break;
                case OsuGameMode.Taiko:
                    CalculateTaiko(input, ref speedTotal, ref accuracyTotal);
                    break;
                case OsuGameMode.Catch:
                    CalculateCatch(input, ref aimTotal, ref accuracyTotal);
                    break;
                case OsuGameMode.Mania:
                    // PlayerSkills keeps the legacy Aim/Speed storage slots for API compatibility.
                    // For mania they store Density/Finger Control respectively.
                    CalculateMania(input, ref aimTotal, ref speedTotal, ref accuracyTotal);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(input.Mode), input.Mode, "Unknown osu! mode.");
            }
        }

        double count = inputs.Length;
        return new PlayerSkills(
            Aim: Sanitize(aimTotal / count * 100),
            Speed: Sanitize(speedTotal / count * 100),
            Accuracy: Sanitize(accuracyTotal / count * 100),
            Stars: Sanitize(starTotal / count),
            CalculatedScores: inputs.Length);
    }

    private static void CalculateOsu(PlayerScoreSkillInput input, ref double aimTotal, ref double speedTotal,
        ref double accuracyTotal)
    {
        double aim = input.AimDifficulty
                     * Math.Pow(input.CircleSize / 4, 0.1)
                     * 2;
        double speed = input.SpeedDifficulty
                       * Math.Pow(input.Bpm / 180, 0.2)
                       * Math.Pow(input.ApproachRate / 6, 0.1)
                       * 2;

        double combinedSkill = aim + speed;
        bool isUnbalanced = combinedSkill > 0
                            && Math.Abs(aim - speed) >
                            Math.Pow(5, Math.Log(combinedSkill) / Math.Log(1.7)) / 2940;
        if (HasAnyMod(input, "DT", "NC") && isUnbalanced)
        {
            aim /= 1.06;
            speed *= Math.Pow(input.StarRating / 5, 0.125);
        }


        // if beatmap is shorter than 90 seconds, nerf aim
        aim *= Math.Min(1, Math.Pow((double)input.HitLength / 90, 0.1));

        // if beatmap is longer than 180 seconds, boost aim and speed
        aim *= Math.Max(1, Math.Pow((double)input.HitLength / 180, 0.1));
        
        double accuracyRatio = Math.Pow(input.AccuracyPercent, 3) / Math.Pow(100, 3);
        double speedNotes = Math.Max(input.SpeedNoteCount, 1);
        double noteFactor = Math.Log10(speedNotes * 900_000_000);
        double comboRatio = (double)input.Combo / Math.Max(input.MaximumCombo, 1);

        double aimExponent = accuracyRatio
                             * (0.083 * noteFactor * (Math.Pow(1.42, comboRatio) - 0.3));
        double speedExponent = accuracyRatio
                               * (0.0945 * noteFactor * (Math.Pow(1.35, comboRatio) - 0.3));
        double accuracy = (Math.Pow(aim / 2, aimExponent) + Math.Pow(speed / 2, speedExponent))
                          * Math.Pow(input.OverallDifficulty / 6, 0.02)
                          * Math.Pow(input.DrainRate / 6, 0.02);

        if (HasAnyMod(input, "FL"))
            accuracy *= 0.095 * noteFactor;

        aimTotal += Sanitize(aim);
        speedTotal += Sanitize(speed);
        accuracyTotal += Sanitize(accuracy);
    }

    /// <summary>
    /// Calculates taiko Stamina instead of nominal Speed. Stamina uses the
    /// available SpeedNoteCount together with BPM, so note density influences
    /// the result instead of BPM alone. The taiko input currently has no
    /// per-mode pattern/syncopation values, so Rhythm/Colour is intentionally
    /// not calculated or exposed as a genuine taiko metric.
    /// </summary>
    private static void CalculateTaiko(PlayerScoreSkillInput input, ref double staminaTotal,
        ref double accuracyTotal)
    {
        double objectCount = Math.Max(input.HitCircleCount + input.SliderCount, 1);
        double beatsPerSecond = Math.Max(input.Bpm / 60.0, 0.001);
        double notesPerSecond = input.SpeedNoteCount
                                / Math.Max(objectCount / beatsPerSecond, 0.001);
        double staminaExponent = 1.0 + Math.Min(notesPerSecond / 8.0, 1.0) * 0.2;
        double stamina = Math.Pow(input.StarRating, staminaExponent);

        double hitWindowGreat = Math.Max(50.0 - 3.0 * input.OverallDifficulty, 0.0);
        double windowFactor = Math.Max(0.0, (50.0 - hitWindowGreat) * 0.012);
        double accuracy = Math.Pow(input.StarRating,
                              Math.Pow(input.AccuracyPercent, 3) / Math.Pow(100, 3) * 1.05)
                          * (1.0 + windowFactor)
                          * Math.Pow(input.DrainRate, 0.02) / Math.Pow(5, 0.02);

        // speedTotal is the legacy PlayerSkills/API slot that stores Stamina for taiko.
        staminaTotal += Sanitize(stamina);
        accuracyTotal += Sanitize(accuracy);
    }

    private static void CalculateCatch(PlayerScoreSkillInput input, ref double aimTotal,
        ref double accuracyTotal)
    {
        double aim = PowWithLogarithmicExponent(input.StarRating, input.Bpm, input.StarRating)
                     * Math.Pow(input.CircleSize, 0.1) / Math.Pow(4, 0.1);
        double accuracy = Math.Pow(input.StarRating,
                              Math.Pow(input.AccuracyPercent, 3.5) / Math.Pow(100, 3.5) * 1.1)
                          * Math.Pow(input.OverallDifficulty, 0.02) / Math.Pow(6, 0.02)
                          * Math.Pow(input.DrainRate, 0.02) / Math.Pow(5, 0.02);

        aimTotal += Sanitize(aim);
        accuracyTotal += Sanitize(accuracy);
    }

    /// <summary>
    /// Calculates mania-specific metrics. Mania has no cursor, so the legacy Aim
    /// and Speed storage slots represent Density and Finger Control instead.
    /// </summary>
    private static void CalculateMania(PlayerScoreSkillInput input, ref double densityTotal,
        ref double fingerControlTotal, ref double accuracyTotal)
    {
        double objectCount = Math.Max(input.HitCircleCount + input.SliderCount, 1);

        double beatsPerSecond = Math.Max(input.Bpm / 60.0, 0.001);
        double notesPerSecond = input.SpeedNoteCount
                                / Math.Max(objectCount / beatsPerSecond, 0.001);
        double densityExponent = 1.0 + Math.Min(notesPerSecond / 10.0, 1.0) * 0.15;
        double density = Math.Pow(input.StarRating, densityExponent);

        double lnRatio = input.SliderCount / objectCount;
        double keyFactor = input.CircleSize / 4.0;
        double fingerControl = input.StarRating
                               * Math.Pow(1.0 + lnRatio, 0.5)
                               * Math.Pow(Math.Max(keyFactor, 0.5), 0.3);

        double hitWindow300 = Math.Max(64.0 - 3.0 * input.OverallDifficulty, 0.0);
        double windowFactor = Math.Max(0.0, (64.0 - hitWindow300) * 0.01);
        double accuracy = Math.Pow(input.StarRating,
                              Math.Pow(input.AccuracyPercent, 3) / Math.Pow(100, 3) * 1.075)
                          * (1.0 + windowFactor)
                          * Math.Pow(input.DrainRate, 0.02) / Math.Pow(5, 0.02);

        densityTotal += Sanitize(density);
        fingerControlTotal += Sanitize(fingerControl);
        accuracyTotal += Sanitize(accuracy);
    }

    private static double PowWithLogarithmicExponent(double value, double bpm, double stars)
    {
        double denominator = Math.Log(stars * 20);
        if (value <= 0 || bpm <= 0 || Math.Abs(denominator) < 0.000_001)
            return 0;

        return Sanitize(Math.Pow(value, Math.Log(bpm) / denominator));
    }

    private static bool HasAnyMod(PlayerScoreSkillInput input, params string[] acronyms) =>
        input.Mods.Any(mod => acronyms.Contains(mod, StringComparer.OrdinalIgnoreCase));

    private static bool IsUsable(PlayerScoreSkillInput input) =>
        input.StarRating > 0
        && input.AccuracyPercent is >= 0 and <= 100
        && (input.Bpm > 0 ||
            ((input.Mode is OsuGameMode.Mania or OsuGameMode.Taiko) && input.Bpm == 0))
        && input.CircleSize >= 0
        && input.ApproachRate >= 0
        && input.OverallDifficulty >= 0
        && input.DrainRate >= 0
        && double.IsFinite(input.StarRating)
        && double.IsFinite(input.AccuracyPercent)
        && double.IsFinite(input.Bpm)
        && double.IsFinite(input.CircleSize)
        && double.IsFinite(input.ApproachRate)
        && double.IsFinite(input.OverallDifficulty)
        && double.IsFinite(input.DrainRate)
        && double.IsFinite(input.AimDifficulty)
        && double.IsFinite(input.SpeedDifficulty)
        && double.IsFinite(input.SpeedNoteCount);

    private static double Sanitize(double value) => double.IsFinite(value) && value >= 0 ? value : 0;
}
