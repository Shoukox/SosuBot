using SosuBot.Graphics.Models;

namespace SosuBot.Graphics;

/// <summary>
/// Legacy skill formulas for non-standard osu! modes. osu!standard cards use
/// <see cref="OsuStandardSkillCalculator"/>, which compares actual and SS
/// osu!lazer performance components.
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
        double speedTotal = 0;
        double accuracyTotal = 0;

        foreach (PlayerScoreSkillInput input in inputs)
        {
            starTotal += input.StarRating;

            switch (input.Mode)
            {
                case OsuGameMode.Taiko:
                    CalculateTaiko(input, ref speedTotal, ref accuracyTotal);
                    break;
                case OsuGameMode.Catch:
                    CalculateCatch(input, ref aimTotal, ref accuracyTotal);
                    break;
                case OsuGameMode.Mania:
                    CalculateMania(input, ref aimTotal, ref speedTotal, ref accuracyTotal);
                    break;
                case OsuGameMode.Osu:
                    throw new InvalidOperationException(
                        "osu!standard skill calculation must use OsuStandardSkillCalculator.");
                default:
                    throw new ArgumentOutOfRangeException(nameof(input.Mode), input.Mode, "Unknown osu! mode.");
            }
        }

        double count = inputs.Length;
        return new PlayerSkills(
            Aim: Sanitize(aimTotal / count * 100),
            Speed: Sanitize(speedTotal * 1.03 / count * 100),
            Accuracy: Sanitize(accuracyTotal / count * 100),
            Stars: Sanitize(starTotal / count),
            CalculatedScores: inputs.Length);
    }

    private static void CalculateTaiko(PlayerScoreSkillInput input, ref double speedTotal,
        ref double accuracyTotal)
    {
        double speed = PowWithLogarithmicExponent(input.StarRating / 1.1, input.Bpm, input.StarRating);
        double accuracy = Math.Pow(input.StarRating,
                              Math.Pow(input.AccuracyPercent, 3) / Math.Pow(100, 3) * 1.05)
                          * Math.Pow(input.OverallDifficulty, 0.02) / Math.Pow(6, 0.02)
                          * Math.Pow(input.DrainRate, 0.02) / Math.Pow(5, 0.02);

        speedTotal += Sanitize(speed);
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

    private static void CalculateMania(PlayerScoreSkillInput input, ref double aimTotal, ref double speedTotal,
        ref double accuracyTotal)
    {
        double aim = PowWithLogarithmicExponent(input.StarRating / 1.1, input.Bpm, input.StarRating);
        double accuracy = Math.Pow(input.StarRating,
                              Math.Pow(input.AccuracyPercent, 3) / Math.Pow(100, 3) * 1.075)
                          * Math.Pow(input.OverallDifficulty, 0.02) / Math.Pow(6, 0.02)
                          * Math.Pow(input.DrainRate, 0.02) / Math.Pow(5, 0.02);

        double objectCount = Math.Max(input.HitCircleCount + input.SliderCount, 1);
        double starLog = Math.Log(Math.Max(input.StarRating * 900, 1.000_001));
        double speedExponent = 1.1
                               * Math.Pow(input.Bpm / 250, 0.4)
                               * (Math.Log(objectCount) / starLog)
                               * Math.Pow(input.OverallDifficulty / 8, 0.4)
                               * Math.Pow(input.DrainRate / 7.5, 0.2)
                               * Math.Pow(input.CircleSize / 4, 0.1);
        double speed = Math.Pow(input.StarRating, speedExponent);

        aimTotal += Sanitize(aim);
        speedTotal += Sanitize(speed);
        accuracyTotal += Sanitize(accuracy);
    }

    private static double PowWithLogarithmicExponent(double value, double bpm, double stars)
    {
        double denominator = Math.Log(stars * 20);
        if (value <= 0 || bpm <= 0 || Math.Abs(denominator) < 0.000_001)
            return 0;

        return Sanitize(Math.Pow(value, Math.Log(bpm) / denominator));
    }

    private static bool IsUsable(PlayerScoreSkillInput input) =>
        input.StarRating > 0
        && input.AccuracyPercent is >= 0 and <= 100
        && input.Bpm > 0
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
