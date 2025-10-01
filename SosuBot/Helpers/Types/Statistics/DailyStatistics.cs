using System.Collections.Concurrent;
using OsuApi.V2.Models;
using OsuApi.V2.Users.Models;

namespace SosuBot.Helpers.Types.Statistics;

/// <summary>
///     Represents a data structure for daily statistics of osu players of a specified country
/// </summary>
/// <param name="CountryCode">Indicates a country from which users are originated</param>
/// <param name="DayOfStatistic">Date when the statistic is relevant for</param>
public record DailyStatistics(string CountryCode, DateTime DayOfStatistic)
{
    public List<User> ActiveUsers { get; set; } = new();
    public List<int> BeatmapsPlayed { get; set; } = new();
    public List<Score> Scores { get; set; } = new();

    public ConcurrentDictionary<int, BeatmapExtended> CachedBeatmapsFromOsuApi { get; } = new();
    public ConcurrentDictionary<int, BeatmapsetExtended> CachedBeatmapsetsFromOsuApi { get; } = new();
}