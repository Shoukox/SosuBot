namespace SosuBot.Graphics.Models;

/// <summary>Everything the replay card shows: the beatmap on top, the play below.</summary>
public sealed class ReplayCardData
{
    public required string Title { get; init; }
    public required string Artist { get; init; }
    public required string DifficultyName { get; init; }
    public required string Mapper { get; init; }

    /// <summary>ranked, approved, qualified, loved, pending, wip or graveyard, as the osu! API writes it.</summary>
    public required string Status { get; init; }

    public OsuGameMode Mode { get; init; }

    /// <summary>The beatmapset cover; a tinted gradient stands in when it is missing or unreadable.</summary>
    public byte[]? Background { get; init; }

    public int LengthSeconds { get; init; }
    public int MaxCombo { get; init; }
    public double Bpm { get; init; }
    public double CircleSize { get; init; }
    public double ApproachRate { get; init; }
    public double Drain { get; init; }
    public double OverallDifficulty { get; init; }
    public double StarRating { get; init; }
    public IReadOnlyList<string> Mods { get; init; } = [];

    public long Score { get; init; }

    /// <summary>
    /// The highest standardised score with these mods (1,000,000 × the mods' score multiplier). The bar through the
    /// score shows how far the score got; a score above it is a classic score, and the bar is simply full.
    /// </summary>
    public long? MaxScore { get; init; }

    public double AccuracyPercent { get; init; }
    public int Combo { get; init; }
    public double? Pp { get; init; }
    public double? FullComboPp { get; init; }
    public double? PerfectPp { get; init; }

    /// <summary>Hit counts in display order, like ("300", 312) or ("Miss", 7).</summary>
    public IReadOnlyList<(string Label, int Count)> Hits { get; init; } = [];
}
