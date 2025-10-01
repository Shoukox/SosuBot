using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OsuApi.V2;
using OsuApi.V2.Clients.Rankings.HttpIO;
using OsuApi.V2.Clients.Scores.HttpIO;
using OsuApi.V2.Clients.Users.HttpIO;
using OsuApi.V2.Models;
using OsuApi.V2.Users.Models;
using SosuBot.Database;
using SosuBot.Helpers;
using SosuBot.Helpers.Comparers;
using SosuBot.Helpers.OutputText;
using SosuBot.Helpers.Types;
using SosuBot.Helpers.Types.Statistics;
using SosuBot.Logging;
using SosuBot.Services.Data.OsuApi;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace SosuBot.Services.BackgroundServices;

public sealed class ScoresObserverBackgroundService(
    ApiV2 osuApi,
    ITelegramBotClient botClient,
    BotContext database,
    ILogger<ScoresObserverBackgroundService> logger) : BackgroundService
{
    public static readonly ConcurrentBag<long> ObservedUsers = new();
    public static List<DailyStatistics> AllDailyStatistics = new();
    public static List<CountryRanking> ActualCountryRankings = new();

    private static readonly ScoreEqualityComparer ScoreComparer = new();

    private static readonly string CacheDirectory =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache", "daily_statistics");

    private static readonly string CachePath =
        Path.Combine(CacheDirectory, "statistics.cache");

    private readonly UserStatisticsCacheDatabase _userDatabase = new(osuApi);

    private long _adminTelegramId;


    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Scores observer background service started");

        try
        {
            _adminTelegramId = (await database.OsuUsers.FirstAsync(m => m.IsAdmin, stoppingToken))
                .TelegramId;
            await AddPlayersToObserverList("uz");
            await AddPlayersToObserverList();

            await Task.WhenAll(
                ObserveScoresGetScores(stoppingToken),
                ObserveScores(stoppingToken),
                CheckOnlineForCountry(stoppingToken)
            );
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Operation cancelled");
        }
    }


    private async Task ObserveScoresGetScores(CancellationToken stoppingToken)
    {
        await LoadDailyStatistics();
        await _userDatabase.CacheIfNeeded();

        DailyStatistics dailyStatistics;
        if (AllDailyStatistics.Count > 0 && AllDailyStatistics.Last().DayOfStatistic.Day == DateTime.UtcNow.Day)
        {
            dailyStatistics = AllDailyStatistics.Last();
        }
        else
        {
            dailyStatistics = new DailyStatistics(CountryCode.Uzbekistan, DateTime.UtcNow);
            AllDailyStatistics.Add(dailyStatistics);
        }

        string? getStdScoresCursor = null;
        string? getManiaScoresCursor = null;
        var counter = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var getStdScoresResponse =
                    await osuApi.Scores.GetScores(new ScoresQueryParameters
                        { CursorString = getStdScoresCursor, Ruleset = Ruleset.Osu });
                var getTaikoScoresResponse =
                    await osuApi.Scores.GetScores(new ScoresQueryParameters
                        { CursorString = getManiaScoresCursor, Ruleset = Ruleset.Taiko });
                var getFruitsScoresResponse =
                    await osuApi.Scores.GetScores(new ScoresQueryParameters
                        { CursorString = getManiaScoresCursor, Ruleset = Ruleset.Fruits });
                var getManiaScoresResponse =
                    await osuApi.Scores.GetScores(new ScoresQueryParameters
                        { CursorString = getManiaScoresCursor, Ruleset = Ruleset.Mania });
                if (getStdScoresResponse == null)
                {
                    logger.LogWarning("getStdScoresResponse returned null");
                    continue;
                }

                if (getManiaScoresResponse == null)
                {
                    logger.LogWarning("getManiaScoresResponse returned null");
                    continue;
                }

                var allOsuScores = getStdScoresResponse.Scores!
                    .Concat(getTaikoScoresResponse?.Scores!)
                    .Concat(getFruitsScoresResponse?.Scores!)
                    .Concat(getManiaScoresResponse.Scores!);

                // Scores only from UZ 
                var uzScores = allOsuScores.Where(m => _userDatabase.ContainsUserStatistics(m.UserId!.Value))
                    .ToArray();
                foreach (var score in uzScores)
                {
                    var userStatistics = await _userDatabase.GetUserStatistics(score.UserId!.Value);
                    if (userStatistics == null)
                    {
                        logger.LogError($"User statistics is null for userId = {score.UserId!.Value}");
                        continue;
                    }

                    dailyStatistics.Scores.Add(score);

                    // ReSharper disable once SimplifyLinqExpressionUseAll
                    if (!dailyStatistics.ActiveUsers.Any(m => m.Id == userStatistics.User!.Id))
                        dailyStatistics.ActiveUsers.Add(userStatistics.User!);

                    // ReSharper disable once SimplifyLinqExpressionUseAll
                    if (!dailyStatistics.BeatmapsPlayed.Any(m => m == score.BeatmapId!.Value))
                        dailyStatistics.BeatmapsPlayed.Add(score.BeatmapId!.Value);
                }

                // New day => send statistics
                if (DateTime.UtcNow.Day != dailyStatistics.DayOfStatistic.Day)
                {
                    var sendText = await ScoreHelper.GetDailyStatisticsSendText(dailyStatistics, osuApi);

                    await botClient.SendMessage(_adminTelegramId, sendText,
                        ParseMode.Html, linkPreviewOptions: true, cancellationToken: stoppingToken);

                    dailyStatistics = new DailyStatistics(CountryCode.Uzbekistan, DateTime.UtcNow);
                    AllDailyStatistics.Add(dailyStatistics);
                }

                // Save every timedelay*100 seconds
                if (counter % 100 == 0) await SaveDailyStatistics();

                counter = (counter + 1) % int.MaxValue;
                getStdScoresCursor = getStdScoresResponse.CursorString;
                getManiaScoresCursor = getManiaScoresResponse.CursorString;
                await Task.Delay(5000, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                logger.LogWarning("Operation cancelled");
                return;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Unexpected exception");
            }
        }
    }

    private async Task ObserveScores(CancellationToken stoppingToken)
    {
        Dictionary<int, GetUserScoresResponse> scores = new();
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                foreach (int userId in ObservedUsers)
                {
                    var userBestScores =
                        await osuApi.Users.GetUserScores(userId, ScoreType.Best,
                            new GetUserScoreQueryParameters { Limit = 50 });
                    if (userBestScores == null)
                    {
                        logger.LogWarning($"{userId} has no scores!");
                        continue;
                    }

                    // ReSharper disable once CanSimplifyDictionaryLookupWithTryGetValue
                    if (scores.ContainsKey(userId))
                    {
                        var newScores =
                            userBestScores.Scores.Except(scores[userId].Scores, ScoreComparer);
                        foreach (var score in newScores)
                        {
                            await botClient.SendMessage(_adminTelegramId,
                                $"<b>{score.User?.Username}</b> set a <b>{score.Pp}pp</b> {ScoreHelper.GetScoreUrlWrappedInString(score.Id!.Value, "score!")}",
                                ParseMode.Html, linkPreviewOptions: true);
                            await Task.Delay(1000, stoppingToken);
                        }
                    }

                    scores[userId] = userBestScores;
                    await Task.Delay(1000, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                logger.LogWarning("Operation cancelled");
                return;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Unexpected exception");
            }
        }

        logger.LogWarning("Finished its work");
    }

    private async Task CheckOnlineForCountry(CancellationToken stoppingToken, string countryCode = "uz")
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var users = await OsuApiHelper.GetUsersFromRanking(osuApi, countryCode, token: stoppingToken);

                var countryRanking = ActualCountryRankings.FirstOrDefault(m => m.CountryCode == countryCode);
                if (countryRanking == null)
                {
                    countryRanking = new CountryRanking(countryCode);
                    ActualCountryRankings.Add(countryRanking);
                }

                countryRanking.StatisticFrom = DateTime.UtcNow;
                countryRanking.Ranking = users!;

                await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("Operation cancelled");
                return;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Unexpected exception");
            }
        }
    }

    /// <summary>
    ///     Get the best players using some filter
    /// </summary>
    /// <param name="countryCode">If null, take from the global ranking</param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    private async Task<UserStatistics[]> GetBestPlayersFor(string? countryCode = null)
    {
        var rankings = await osuApi.Rankings.GetRanking(Ruleset.Osu, RankingType.Performance,
            new GetRankingQueryParameters { Country = countryCode, Filter = Filter.All });
        if (rankings == null)
        {
            logger.LogError($"Ranking is null. {countryCode}");
            throw new Exception("Ranking is null. See logs for details");
        }

        return rankings.Ranking!;
    }

    /// <summary>
    ///     Add players to the <see cref="ObservedUsers" />
    /// </summary>
    /// <param name="countryCode">If null, take from global ranking</param>
    /// <param name="count">Amount of players to add</param>
    private async Task AddPlayersToObserverList(string? countryCode = null, int count = 50)
    {
        var bestPlayersStatistics = (await GetBestPlayersFor(countryCode)).Take(count).ToArray();
        foreach (var playerStatistics in bestPlayersStatistics) ObservedUsers.Add(playerStatistics.User!.Id.Value);
    }

    private async Task SaveDailyStatistics()
    {
        if (!Directory.Exists(CacheDirectory)) Directory.CreateDirectory(CacheDirectory);

        await File.WriteAllTextAsync(CachePath, JsonSerializer.Serialize(AllDailyStatistics));
    }

    private async Task LoadDailyStatistics()
    {
        if (!File.Exists(CachePath)) return;

        AllDailyStatistics = JsonSerializer.Deserialize<List<DailyStatistics>>(await File.ReadAllTextAsync(CachePath))!;
    }
}