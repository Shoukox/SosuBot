namespace SosuBot.Graphics.Models;

/// <summary>What the /scorep picture shows over the beatmap's background: the statistics of one score.</summary>
public sealed class ScoreCardData
{
    /// <summary>The beatmap's background; the picture takes its size. A gradient stands in when it is unreadable.</summary>
    public byte[]? Background { get; init; }

    public OsuGameMode Mode { get; init; }

    /// <summary>XH, X, SH, S, A, B, C, D or F, as the osu! API writes it.</summary>
    public required string Rank { get; init; }

    public IReadOnlyList<string> Mods { get; init; } = [];

    public long Score { get; init; }

    /// <summary>The highest standardised score with these mods; the bar through the score ends there.</summary>
    public long? MaxScore { get; init; }

    public double AccuracyPercent { get; init; }
    public int Combo { get; init; }
    public int MaxCombo { get; init; }
    public double? Pp { get; init; }
    public double? FullComboPp { get; init; }
    public double? PerfectPp { get; init; }

    /// <summary>Hit counts in display order, like ("300", 312) or ("Miss", 7).</summary>
    public IReadOnlyList<(string Label, int Count)> Hits { get; init; } = [];
}
