namespace SosuBot.Configuration;

public sealed record BeatmapsConfiguration
{
    public string CacheDirectory { get; init; } = Path.Combine(AppContext.BaseDirectory, "cache", "beatmaps");
    public string? IndexFilePath { get; init; }
}
