using OsuApi.BanchoV2.Models;
using OsuApi.BanchoV2.Users.Models;

namespace SosuBot.Helpers.Types.Statistics
{
    public record DailyStats(string CountryCode, DateTime DayOfStatistic)
    {
        public List<User> ActiveUsers { get; set; } = new();
        public List<int> BeatmapsPlayed { get; set; } = new();
        public List<Score> Scores { get; set; } = new();
    }
}
