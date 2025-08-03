using System.Text.Json;
using OsuApi.V2;
using OsuApi.V2.Users.Models;
using SosuBot.Helpers;
using SosuBot.Helpers.Types;

namespace SosuBot.Services.Data.OsuApi;

public class UserStatisticsCacheDatabase(ApiV2 api, string? usersCachePath = null)
{
    public const int CachingDays = 31;

    private readonly SemaphoreSlim _semaphoreSlim = new(1, 1);

    public ApiV2 Api { get; } = api;
    private string UsersCachePath { get; } = usersCachePath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache", "users");

    public void CreateCacheDirectoryIfNeeded()
    {
        if (!Directory.Exists(UsersCachePath)) Directory.CreateDirectory(UsersCachePath);
    }

    public bool ContainsUserStatistics(int userId)
    {
        if (!File.Exists(GetCachedUserStatisticsPath(userId))) return false;

        return true;
    }

    public async Task CacheIfNeeded()
    {
        await _semaphoreSlim.WaitAsync();
        if (!Directory.Exists(UsersCachePath) ||
            (Directory.GetFiles(UsersCachePath) is { } files && files.Length == 0) ||
            (Directory.GetFiles(UsersCachePath) is { } foundFiles &&
             IsUserStatisticsCacheExpired(int.Parse(Path.GetFileName(foundFiles[0]).Split('.')[0]))))
            await CacheUsersFromGivenCountry(CountryCode.Uzbekistan);
        _semaphoreSlim.Release();
    }

    /// <summary>
    ///     Gets user statistics from cache
    /// </summary>
    /// <param name="userId">Osu user id</param>
    /// <returns>Null if not exists</returns>
    public async Task<UserStatistics?> GetUserStatistics(int userId)
    {
        await CacheIfNeeded();

        if (!File.Exists(GetCachedUserStatisticsPath(userId))) return null;

        return JsonSerializer.Deserialize<UserStatistics>(
            await File.ReadAllTextAsync(GetCachedUserStatisticsPath(userId)));
    }

    /// <summary>
    ///     Caches all users for a given country and saves them at the default cache path
    /// </summary>
    /// <param name="country">See <see cref="CountryCode" /></param>
    private async Task CacheUsersFromGivenCountry(string country)
    {
        CreateCacheDirectoryIfNeeded();

        var users = await OsuApiHelper.GetUsersFromRanking(Api, country);
        if (users == null)
        {
            throw new Exception("Could not get users from ranking");
        }
        
        foreach (var user in users)
            await File.WriteAllTextAsync(GetCachedUserStatisticsPath(user.User!.Id.Value),
                JsonSerializer.Serialize(user, new JsonSerializerOptions { WriteIndented = false }));
    }

    private string GetCachedUserStatisticsPath(int userId)
    {
        return Path.Combine(UsersCachePath, $"{userId}.cache");
    }

    private bool IsUserStatisticsCacheExpired(int userId)
    {
        var lastModified = File.GetLastWriteTime(GetCachedUserStatisticsPath(userId));
        return (DateTime.Now - lastModified).TotalDays > CachingDays;
    }
}