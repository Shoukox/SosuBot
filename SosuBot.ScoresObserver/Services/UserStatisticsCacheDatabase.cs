using OsuApi.BanchoV2;
using OsuApi.BanchoV2.Clients.Rankings.HttpIO;
using OsuApi.BanchoV2.Models;
using OsuApi.BanchoV2.Users.Models;
using SosuBot.Database.Models;
using SosuBot.ScoresObserver.Models;
using System.Text.Json;

namespace SosuBot.ScoresObserver.Services;

public class UserStatisticsCacheDatabase(BanchoApiV2 api, string? usersCachePath = null)
{
    public const int CachingDays = 1;

    private readonly SemaphoreSlim _semaphoreSlim = new(1, 1);

    public BanchoApiV2 Api { get; } = api;

    private string UsersCachePath { get; } =
        usersCachePath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache", "users");

    public void CreateCacheDirectoryIfNeeded()
    {
        if (!Directory.Exists(UsersCachePath)) Directory.CreateDirectory(UsersCachePath);
    }

    public bool ContainsUserStatistics(int userId)
    {
        return File.Exists(GetCachedUserStatisticsPath(userId));
    }

    public async Task CacheIfNeeded(CancellationToken token = default)
    {
        await _semaphoreSlim.WaitAsync(token);
        try
        {
            if (!Directory.Exists(UsersCachePath) ||
                (Directory.GetFiles(UsersCachePath) is { } files && files.Length == 0) ||
                (Directory.GetFiles(UsersCachePath) is { } foundFiles &&
                 IsUserStatisticsCacheExpired(int.Parse(Path.GetFileNameWithoutExtension(foundFiles[0])))))
            {
                await CacheUsersFromGivenCountry(CountryCode.Uzbekistan, token);

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

    public async Task<UserStatistics?> GetUserStatistics(int userId, CancellationToken token = default)
    {
        await CacheIfNeeded(token);

        if (!File.Exists(GetCachedUserStatisticsPath(userId))) return null;

        return JsonSerializer.Deserialize<UserStatistics>(
            await File.ReadAllTextAsync(GetCachedUserStatisticsPath(userId), token));
    }

    private async Task CacheUsersFromGivenCountry(string country, CancellationToken token = default)
    {
        CreateCacheDirectoryIfNeeded();

        Task<List<UserStatistics>?>[] getRankingTasks =
        [
            GetUsersFromRanking(Api, Playmode.Osu, country, token: token),
            GetUsersFromRanking(Api, Playmode.Taiko, country, token: token),
            GetUsersFromRanking(Api, Playmode.Catch, country, token: token),
            GetUsersFromRanking(Api, Playmode.Mania, country, token: token)
        ];
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
        {
            await File.WriteAllTextAsync(GetCachedUserStatisticsPath(user!.User!.Id.Value),
                JsonSerializer.Serialize(user, new JsonSerializerOptions { WriteIndented = false }), token);
        }
    }

    private static async Task<List<UserStatistics>?> GetUsersFromRanking(BanchoApiV2 api, Playmode playmode, string? countryCode, int? count = null, CancellationToken token = default)
    {
        var users = new List<UserStatistics>();
        var page = 1;

        while (!token.IsCancellationRequested)
        {
            var ranking = await api.Rankings.GetRanking(ToRuleset(playmode), RankingType.Performance,
                new GetRankingQueryParameters { Country = countryCode, CursorPage = page });

            if (ranking == null) return null;

            foreach (var userStatistics in ranking.Ranking!) users.Add(userStatistics);

            if (ranking.Cursor == null) break;
            if (count != null && count.Value <= page * 50) break;

            page += 1;
            await Task.Delay(1000, token);
        }

        return count != null ? users.Take(count.Value).ToList() : users;
    }

    private static string ToRuleset(Playmode playmode)
    {
        return playmode switch
        {
            Playmode.Osu => Ruleset.Osu,
            Playmode.Taiko => Ruleset.Taiko,
            Playmode.Catch => Ruleset.Fruits,
            Playmode.Mania => Ruleset.Mania,
            _ => Ruleset.Osu
        };
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
            catch
            {
                // ignore malformed cache entry names
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
