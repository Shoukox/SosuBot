namespace Sosu.PPCalc
{
    public interface IBeatmapOnlineInfo
    {
        /// <summary>
        /// The max combo of this beatmap.
        /// </summary>
        int? MaxCombo { get; }

        /// <summary>
        /// The approach rate.
        /// </summary>
        float ApproachRate { get; }

        /// <summary>
        /// The circle size.
        /// </summary>
        float CircleSize { get; }

        /// <summary>
        /// The drain rate.
        /// </summary>
        float DrainRate { get; }

        /// <summary>
        /// The overall difficulty.
        /// </summary>
        float OverallDifficulty { get; }

        /// <summary>
        /// The amount of circles in this beatmap.
        /// </summary>
        int CircleCount { get; }

        /// <summary>
        /// The amount of sliders in this beatmap.
        /// </summary>
        int SliderCount { get; }

        /// <summary>
        /// The amount of spinners in tihs beatmap.
        /// </summary>
        int SpinnerCount { get; }

        /// <summary>
        /// The amount of plays this beatmap has.
        /// </summary>
        int PlayCount { get; }

        /// <summary>
        /// The amount of passes this beatmap has.
        /// </summary>
        int PassCount { get; }

        /// <summary>
        /// The playable length in milliseconds of this beatmap.
        /// </summary>
        double HitLength { get; }
    }
}
