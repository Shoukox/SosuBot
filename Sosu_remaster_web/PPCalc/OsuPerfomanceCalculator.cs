using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Sosu.Types;
using System.Xml.Linq;
using Sosu.PPCalc;

namespace Sosu.PPCalc
{
    public class OsuPerformanceCalculator
    {
        public const double PERFORMANCE_BASE_MULTIPLIER = 1.15; // This is being adjusted to keep the final pp value scaled around what it used to be when changing things.

        private bool usingClassicSliderAccuracy;

        private double accuracy;
        private int scoreMaxCombo;
        private int countGreat;
        private int countOk;
        private int countMeh;
        private int countMiss;

        /// <summary>
        /// Missed slider ticks that includes missed reverse arrows. Will only be correct on non-classic scores
        /// </summary>
        private int countSliderTickMiss;

        /// <summary>
        /// Amount of missed slider tails that don't break combo. Will only be correct on non-classic scores
        /// </summary>
        private int countSliderEndsDropped;

        /// <summary>
        /// Estimated total amount of combo breaks
        /// </summary>
        private double effectiveMissCount;

        public OsuPerformanceAttributes CreatePerformanceAttributes(Statistics score, DifficultyAttributes attributes)
        {
            // doesn't support relax, lazer 

            usingClassicSliderAccuracy = true; //classic slider acc

            accuracy = score.Accuracy;
            scoreMaxCombo = score.MaxCombo;
            countGreat = score.Hit300;
            countOk = score.Hit100;
            countMeh = score.Hit50;
            countMiss = score.Miss;
            countSliderEndsDropped = attributes.MaxCombo - score.MaxCombo;
            effectiveMissCount = countMiss;

            if (osuAttributes.SliderCount > 0)
            {
                if (usingClassicSliderAccuracy)
                {
                    // Consider that full combo is maximum combo minus dropped slider tails since they don't contribute to combo but also don't break it
                    // In classic scores we can't know the amount of dropped sliders so we estimate to 10% of all sliders on the map
                    double fullComboThreshold = attributes.MaxCombo - 0.1 * osuAttributes.SliderCount;

                    if (scoreMaxCombo < fullComboThreshold)
                        effectiveMissCount = fullComboThreshold / Math.Max(1.0, scoreMaxCombo);

                    // In classic scores there can't be more misses than a sum of all non-perfect judgements
                    effectiveMissCount = Math.Min(effectiveMissCount, totalImperfectHits);
                }
                else
                {
                    double fullComboThreshold = attributes.MaxCombo - countSliderEndsDropped;

                    if (scoreMaxCombo < fullComboThreshold)
                        effectiveMissCount = fullComboThreshold / Math.Max(1.0, scoreMaxCombo);

                    // Combine regular misses with tick misses since tick misses break combo as well
                    effectiveMissCount = Math.Min(effectiveMissCount, countSliderTickMiss + countMiss);
                }
            }

            effectiveMissCount = Math.Max(countMiss, effectiveMissCount);
            effectiveMissCount = Math.Min(totalHits, effectiveMissCount);

            double multiplier = PERFORMANCE_BASE_MULTIPLIER;

            if (score.Mods.HasFlag(osu.V1.Enums.Mods.NoFail))
                multiplier *= Math.Max(0.90, 1.0 - 0.02 * effectiveMissCount);

            if (score.Mods.HasFlag(osu.V1.Enums.Mods.SpunOut) && totalHits > 0)
                multiplier *= 1.0 - Math.Pow((double)osuAttributes.SpinnerCount / totalHits, 0.85);

            double aimValue = computeAimValue(score, osuAttributes);
            double speedValue = computeSpeedValue(score, osuAttributes);
            double accuracyValue = computeAccuracyValue(score, osuAttributes);
            double flashlightValue = computeFlashlightValue(score, osuAttributes);
            double totalValue =
                Math.Pow(
                    Math.Pow(aimValue, 1.1) +
                    Math.Pow(speedValue, 1.1) +
                    Math.Pow(accuracyValue, 1.1) +
                    Math.Pow(flashlightValue, 1.1), 1.0 / 1.1
                ) * multiplier;

            return new OsuPerformanceAttributes
            {
                Aim = aimValue,
                Speed = speedValue,
                Accuracy = accuracyValue,
                Flashlight = flashlightValue,
                EffectiveMissCount = effectiveMissCount,
                Total = totalValue
            };
        }

        private double computeAimValue(Statistics score, DifficultyAttributes attributes)
        {
            double aimValue = computeStrainSkill(attributes.AimDifficulty);

            double lengthBonus = 0.95 + 0.4 * Math.Min(1.0, totalHits / 2000.0) +
                                 (totalHits > 2000 ? Math.Log10(totalHits / 2000.0) * 0.5 : 0.0);
            aimValue *= lengthBonus;

            if (effectiveMissCount > 0)
                aimValue *= calculateMissPenalty(effectiveMissCount, attributes.AimDifficultStrainCount);

            double approachRateFactor = 0.0;
            if (attributes.ApproachRate > 10.33)
                approachRateFactor = 0.3 * (attributes.ApproachRate - 10.33);
            else if (attributes.ApproachRate < 8.0)
                approachRateFactor = 0.05 * (8.0 - attributes.ApproachRate);

            aimValue *= 1.0 + approachRateFactor * lengthBonus; // Buff for longer maps with high AR.
            aimValue *= 1.3 + (totalHits * (0.0016 / (1 + 2 * effectiveMissCount)) * Math.Pow(accuracy, 16)) * (1 - 0.003 * attributes.DrainRate * attributes.DrainRate);
            
            if (score.Mods.HasFlag(osu.V1.Enums.Mods.Hidden))
            {
                // We want to give more reward for lower AR when it comes to aim and HD. This nerfs high AR and buffs lower AR.
                aimValue *= 1.0 + 0.04 * (12.0 - attributes.ApproachRate);
            }

            // We assume 15% of sliders in a map are difficult since there's no way to tell from the performance calculator.
            double estimateDifficultSliders = attributes.SliderCount * 0.15;

            if (attributes.SliderCount > 0)
            {
                double estimateImproperlyFollowedDifficultSliders;

                if (usingClassicSliderAccuracy)
                {
                    // When the score is considered classic (regardless if it was made on old client or not) we consider all missing combo to be dropped difficult sliders
                    int maximumPossibleDroppedSliders = totalImperfectHits;
                    estimateImproperlyFollowedDifficultSliders = Math.Clamp(Math.Min(maximumPossibleDroppedSliders, attributes.MaxCombo - scoreMaxCombo), 0, estimateDifficultSliders);
                }
                else
                {
                    // We add tick misses here since they too mean that the player didn't follow the slider properly
                    // We however aren't adding misses here because missing slider heads has a harsh penalty by itself and doesn't mean that the rest of the slider wasn't followed properly
                    estimateImproperlyFollowedDifficultSliders = Math.Clamp(countSliderEndsDropped + countSliderTickMiss, 0, estimateDifficultSliders);
                }

                double sliderNerfFactor = (1 - attributes.SliderFactor) * Math.Pow(1 - estimateImproperlyFollowedDifficultSliders / estimateDifficultSliders, 3) + attributes.SliderFactor;
                aimValue *= sliderNerfFactor;
            }

            aimValue *= accuracy;
            // It is important to consider accuracy difficulty when scaling with accuracy.
            aimValue *= 0.98 + Math.Pow(attributes.OverallDifficulty, 2) / 2500;

            return aimValue;
        }

        private double computeSpeedValue(Statistics score, DifficultyAttributes attributes)
        {
            double speedValue = computeStrainSkill(attributes.SpeedDifficulty);

            double lengthBonus = 0.95 + 0.4 * Math.Min(1.0, totalHits / 2000.0) +
                                 (totalHits > 2000 ? Math.Log10(totalHits / 2000.0) * 0.5 : 0.0);
            speedValue *= lengthBonus;

            if (effectiveMissCount > 0)
                speedValue *= calculateMissPenalty(effectiveMissCount, attributes.SpeedDifficultStrainCount);

            double approachRateFactor = 0.0;
            if (attributes.ApproachRate > 10.33)
                approachRateFactor = 0.3 * (attributes.ApproachRate - 10.33);

            speedValue *= 1.0 + approachRateFactor * lengthBonus; // Buff for longer maps with high AR.

            if (score.Mods.HasFlag(osu.V1.Enums.Mods.Hidden))
            {
                // We want to give more reward for lower AR when it comes to aim and HD. This nerfs high AR and buffs lower AR.
                speedValue *= 1.0 + 0.04 * (12.0 - attributes.ApproachRate);
            }

            // Calculate accuracy assuming the worst case scenario
            double relevantTotalDiff = totalHits - attributes.SpeedNoteCount;
            double relevantCountGreat = Math.Max(0, countGreat - relevantTotalDiff);
            double relevantCountOk = Math.Max(0, countOk - Math.Max(0, relevantTotalDiff - countGreat));
            double relevantCountMeh = Math.Max(0, countMeh - Math.Max(0, relevantTotalDiff - countGreat - countOk));
            double relevantAccuracy = attributes.SpeedNoteCount == 0 ? 0 : (relevantCountGreat * 6.0 + relevantCountOk * 2.0 + relevantCountMeh) / (attributes.SpeedNoteCount * 6.0);

            // Scale the speed value with accuracy and OD.
            speedValue *= (0.95 + Math.Pow(attributes.OverallDifficulty, 2) / 750) * Math.Pow((accuracy + relevantAccuracy) / 2.0, (14.5 - attributes.OverallDifficulty) / 2);

            // Scale the speed value with # of 50s to punish doubletapping.
            speedValue *= Math.Pow(0.99, countMeh < totalHits / 500.0 ? 0 : countMeh - totalHits / 500.0);

            return speedValue;
        }

        private double computeAccuracyValue(Statistics score, DifficultyAttributes attributes)
        {
            // This percentage only considers HitCircles of any value - in this part of the calculation we focus on hitting the timing hit window.
            double betterAccuracyPercentage;
            int amountHitObjectsWithAccuracy = attributes.HitCircleCount;
            if (!usingClassicSliderAccuracy)
                amountHitObjectsWithAccuracy += attributes.SliderCount;

            if (amountHitObjectsWithAccuracy > 0)
                betterAccuracyPercentage = ((countGreat - (totalHits - amountHitObjectsWithAccuracy)) * 6 + countOk * 2 + countMeh) / (double)(amountHitObjectsWithAccuracy * 6);
            else
                betterAccuracyPercentage = 0;

            // It is possible to reach a negative accuracy with this formula. Cap it at zero - zero points.
            if (betterAccuracyPercentage < 0)
                betterAccuracyPercentage = 0;

            // Lots of arbitrary values from testing.
            // Considering to use derivation from perfect accuracy in a probabilistic manner - assume normal distribution.
            double accuracyValue = Math.Pow(1.52163, attributes.OverallDifficulty) * Math.Pow(betterAccuracyPercentage, 24) * 2.83;

            // Bonus for many hitcircles - it's harder to keep good accuracy up for longer.
            accuracyValue *= Math.Min(1.15, Math.Pow(amountHitObjectsWithAccuracy / 1000.0, 0.3));

            // Increasing the accuracy value by object count for Blinds isn't ideal, so the minimum buff is given.
            if (score.Mods.HasFlag(osu.V1.Enums.Mods.Hidden))
                accuracyValue *= 1.08;

            if (score.Mods.HasFlag(osu.V1.Enums.Mods.Flashlight))
                accuracyValue *= 1.02;

            return accuracyValue;
        }

        // Miss penalty assumes that a player will miss on the hardest parts of a map,
        // so we use the amount of relatively difficult sections to adjust miss penalty
        // to make it more punishing on maps with lower amount of hard sections.
        private double calculateMissPenalty(double missCount, double difficultStrainCount) => 0.96 / ((missCount / (4 * Math.Pow(Math.Log(difficultStrainCount), 0.94))) + 1);
        private double getComboScalingFactor(DifficultyAttributes attributes) => attributes.MaxCombo <= 0 ? 1.0 : Math.Min(Math.Pow(scoreMaxCombo, 0.8) / Math.Pow(attributes.MaxCombo, 0.8), 1.0);
        private int totalHits => countGreat + countOk + countMeh + countMiss;
        private int totalImperfectHits => countOk + countMeh + countMiss;

        public double computeStrainSkill(double diff) => Math.Pow(5.0 * Math.Max(1.0, diff / 0.0675) - 4.0, 3.0) / 100000.0;
        public static double computeFlashlightSkill(double difficulty) => 25 * Math.Pow(difficulty, 2);
    }
}