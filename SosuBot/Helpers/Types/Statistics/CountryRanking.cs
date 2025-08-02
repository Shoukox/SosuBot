using OsuApi.V2.Users.Models;

namespace SosuBot.Helpers.Types.Statistics;

public record CountryRanking(string CountryCode, DateTime StatisticFrom)
{
    public List<UserStatistics> Ranking { get; set; } = new();
}