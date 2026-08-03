namespace SosuBot.Graphics.Models;

public sealed class ScorePreviewData
{
    public required string BeatmapTitle { get; init; }
    public required string DifficultyName { get; init; }
    public required string Username { get; init; }
    public required string Rank { get; init; }

    public byte[]? BackgroundImage { get; init; }
    public byte[]? AvatarImage { get; init; }
    public byte[]? CountryFlagImage { get; init; }
    public string? CountryCode { get; init; }
    public int? CountryRank { get; init; }

    public bool IsFullCombo { get; init; }
    public int Misses { get; init; }
    public double? PerformancePoints { get; init; }
    public int Combo { get; init; }
    public double AccuracyPercent { get; init; }
    public double StarRating { get; init; }
    public double Bpm { get; init; }
    public IReadOnlyList<string> Mods { get; init; } = [];
}
