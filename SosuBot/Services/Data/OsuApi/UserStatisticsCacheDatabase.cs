using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OsuApi.V2;
using OsuApi.V2.Models;
using OsuApi.V2.Users.Models;
using SosuBot.Helpers.Types;

namespace SosuBot.Services.Data.OsuApi;

public class UserStatisticsCacheDatabase
{
    public ApiV2 api { get; }
    private string UsersCachePath { get; }

    public const int CACHING_DAYS = 31;

    private SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1, 1);

    public UserStatisticsCacheDatabase(ApiV2 api, string? usersCachePath = null)
    {
        this.api = api;
        UsersCachePath = usersCachePath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache", "users");
    }

    public void CreateCacheDirectoryIfNeeded()
    {
        if (!Directory.Exists(UsersCachePath))
        {
            Directory.CreateDirectory(UsersCachePath);
        }
    }

    public bool ContainsUserStatistics(int userId)
    {
        if (!File.Exists(GetCachedUserStatisticsPath(userId)))
        {
            return false;
        }

        return true;
    }

    public async Task CacheIfNeeded()
    {
        await _semaphoreSlim.WaitAsync();
        if (!Directory.Exists(UsersCachePath) || 
            (Directory.GetFiles(UsersCachePath) is { } files && files.Length == 0) ||
            (Directory.GetFiles(UsersCachePath) is { } foundFiles && IsUserStatisticsCacheExpired(int.Parse(Path.GetFileName(foundFiles[0]).Split('.')[0]))))
        {
            await CacheUsersFromGivenCountry(CountryCode.Uzbekistan);
        }
        _semaphoreSlim.Release();
    }

    /// <summary>
    /// Gets user statistics from cache
    /// </summary>
    /// <param name="userId">Osu user id</param>
    /// <returns>Null if not exists</returns>
    public async Task<UserStatistics?> GetUserStatistics(int userId)
    {
        await CacheIfNeeded();

        if (!File.Exists(GetCachedUserStatisticsPath(userId)))
        {
            return null;
        }

        return JsonSerializer.Deserialize<UserStatistics>(
            await File.ReadAllTextAsync(GetCachedUserStatisticsPath(userId)));
    }

    /// <summary>
    /// Caches all users for a given country and saves them at the default cache path
    /// </summary>
    /// <param name="country">See <see cref="CountryCode"/></param>
    private async Task CacheUsersFromGivenCountry(string country)
    {
        CreateCacheDirectoryIfNeeded();

        List<UserStatistics> users = await GetUsersFromSpecificCountry(country);
        foreach (var user in users)
        {
            await File.WriteAllTextAsync(GetCachedUserStatisticsPath(user.User!.Id.Value),
                JsonSerializer.Serialize(user, new JsonSerializerOptions { WriteIndented = false }));
        }
    }

    /// <summary>
    /// Gets all users from country ranking
    /// </summary>
    /// <param name="country">See <see cref="CountryCode"/></param>
    /// <returns></returns>
    private async Task<List<UserStatistics>> GetUsersFromSpecificCountry(string country)
    {
        List<UserStatistics> users = new List<UserStatistics>();

        int page = 1;
        while (true)
        {
            Rankings? ranking = await api.Rankings.GetRanking(Ruleset.Osu, RankingType.Performance,
                new() { Country = "uz", CursorPage = page });

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
            page += 1;

            await Task.Delay(1000);
        }

        return users;
    }

    private string GetCachedUserStatisticsPath(int userId)
    {
        return Path.Combine(UsersCachePath, $"{userId}.cache");
    }

    private bool IsUserStatisticsCacheExpired(int userId)
    {
        DateTime lastModified = File.GetLastWriteTime(GetCachedUserStatisticsPath(userId));
        return (DateTime.Now - lastModified).TotalDays > CACHING_DAYS;
    }
}