using OsuApi.V2.Users.Models;

namespace SosuBot.Helpers.Types.Statistics;

public record CountryRanking(string CountryCode)
{
    public DateTime StatisticFrom { get; set; } =  DateTime.UtcNow;
    public List<UserStatistics> Ranking { get; set; } = new();
}