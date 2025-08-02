using Microsoft.Extensions.Logging;
using OsuApi.V2;
using OsuApi.V2.Models;
using OsuApi.V2.Users.Models;
using SosuBot.Helpers.Types;

namespace SosuBot.Helpers;

public static class OsuApiHelper
{
    /// <summary>
    /// Gets all users from country ranking. Sends a lot of osu!api v2 requests!
    /// </summary>
    /// <param name="api">osu!api v2 instance</param>
    /// <param name="countryCode">See <see cref="CountryCode"/></param>
    /// <param name="count">How much players to return. If null, return the whole ranking</param>
    /// <returns></returns>
    public static async Task<List<UserStatistics>> GetUsersFromRanking(ApiV2 api, string? countryCode = "uz", int? count = null)
    {
        List<UserStatistics> users = new List<UserStatistics>();

        int page = 1;
        while (true)
        {
            Rankings? ranking = await api.Rankings.GetRanking(Ruleset.Osu, RankingType.Performance,
                new() { Country = countryCode, CursorPage = page });

            if (ranking == null)
            {
                api.Logger.LogWarning("Ranking is null");
                continue;
            }

            foreach (var userStatistics in ranking.Ranking!)
            {
                users.Add(userStatistics);
            }

            if (ranking.Cursor == null) break;
            if (count != null && count.Value <= page * 50)
            {
                break;
            }
            
            page += 1;
            await Task.Delay(1000);
        }

        if (count != null)
        {
            return users.Take(count.Value).ToList();
        }
        
        return users;
    }
}