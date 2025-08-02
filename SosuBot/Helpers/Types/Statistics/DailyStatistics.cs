using System.Collections.Concurrent;
using OsuApi.V2.Models;
using OsuApi.V2.Users.Models;

namespace SosuBot.Helpers.Types.Statistics;

/// <summary>
///     Represents a structure for daily statistics about osu players
/// </summary>
/// <param name="CountryCode">Indicates a country from which users are originated</param>
/// <param name="DayOfStatistic">Date when the statistic is relevant for</param>
public record DailyStatistics(string CountryCode, DateTime DayOfStatistic)
{
    public List<User> ActiveUsers { get; set; } = new();
    public List<int> BeatmapsPlayed { get; set; } = new();
    public List<Score> Scores { get; set; } = new();

    public ConcurrentDictionary<int, BeatmapExtended> CachedBeatmapsFromOsuApi { get; set; } = new();
    public ConcurrentDictionary<int, BeatmapsetExtended> CachedBeatmapsetsFromOsuApi { get; set; } = new();
}