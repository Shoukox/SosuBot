using OsuApi.V2.Models;

namespace SosuBot.Helpers.Types;

public sealed record ObservedScoresStatistics(DateTime StatisticsDateTime)
{
    public int ScoresCount { get; set; }
    public List<Score> Scores { get; set; } = new();
}