using System.Text.Json;
using OsuApi.V2;
using OsuApi.V2.Users.Models;
using SosuBot.Helpers;
using SosuBot.Helpers.Types;

namespace SosuBot.Services;

/// <summary>
///     Database for UZ osu!players. // in the future is planned to expand this database for storing not only the UZ
///     players
/// </summary>
/// <param name="api"></param>
/// <param name="usersCachePath"></param>
public class UserStatisticsCacheDatabase(ApiV2 api, string? usersCachePath = null)
{
    public const int CachingDays = 1;

    private readonly SemaphoreSlim _semaphoreSlim = new(1, 1);

    public ApiV2 Api { get; } = api;

    private string UsersCachePath { get; } =
        usersCachePath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache", "users");

    public void CreateCacheDirectoryIfNeeded()
    {
        if (!Directory.Exists(UsersCachePath)) Directory.CreateDirectory(UsersCachePath);
    }

    /// <summary>
    ///     If a specified userId was already cached (userId should be an UZ player)
    /// </summary>
    /// <param name="userId">user id</param>
    /// <returns>true if cached</returns>
    public bool ContainsUserStatistics(int userId)
    {
        return File.Exists(GetCachedUserStatisticsPath(userId));
    }

    public async Task CacheIfNeeded()
    {
        await _semaphoreSlim.WaitAsync();
        try
        {
            if (!Directory.Exists(UsersCachePath) ||
                (Directory.GetFiles(UsersCachePath) is { } files && files.Length == 0) ||
                (Directory.GetFiles(UsersCachePath) is { } foundFiles &&
                 IsUserStatisticsCacheExpired(int.Parse(Path.GetFileNameWithoutExtension(foundFiles[0])))))
            {
                await CacheUsersFromGivenCountry(CountryCode.Uzbekistan);

                if (Directory.GetFiles(UsersCachePath) is { } foundCachedUsers)
                {
                    RemoveUnnecessaryCachedUsers(foundCachedUsers);
                }
            }
        }
        finally
        {
            _semaphoreSlim.Release();
        }
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

        Task<List<UserStatistics>?>[] getRankingTasks =
        {
            OsuApiHelper.GetUsersFromRanking(Api, Playmode.Osu, country),
            OsuApiHelper.GetUsersFromRanking(Api, Playmode.Taiko, country),
            OsuApiHelper.GetUsersFromRanking(Api, Playmode.Catch, country),
            OsuApiHelper.GetUsersFromRanking(Api, Playmode.Mania, country)
        };
        await Task.WhenAll(getRankingTasks);

        var usersStd = getRankingTasks[0].Result;
        var usersTaiko = getRankingTasks[1].Result;
        var usersCatch = getRankingTasks[2].Result;
        var usersMania = getRankingTasks[3].Result;

        if (usersStd == null || usersTaiko == null || usersCatch == null || usersMania == null)
            throw new Exception("Could not get users from ranking");

        List<UserStatistics?> users = [];
        users.AddRange(usersStd);
        users.AddRange(usersTaiko);
        users.AddRange(usersCatch);
        users.AddRange(usersMania);

        users = users.DistinctBy(m => m!.User!.Id).ToList();

        foreach (var user in users)
            await File.WriteAllTextAsync(GetCachedUserStatisticsPath(user!.User!.Id.Value),
                JsonSerializer.Serialize(user, new JsonSerializerOptions { WriteIndented = false }));
    }

    private void RemoveUnnecessaryCachedUsers(string[] foundCachedUsersFiles)
    {
        foreach (string file in foundCachedUsersFiles)
        {
            try
            {
                if (IsUserStatisticsCacheExpired(int.Parse(Path.GetFileNameWithoutExtension(file))))
                {
                    File.Delete(file);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);   
            }
        }
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