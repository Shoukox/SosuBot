using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OsuApi.V2;
using OsuApi.V2.Clients.Scores.HttpIO;
using OsuApi.V2.Clients.Users.HttpIO;
using OsuApi.V2.Models;
using OsuApi.V2.Users.Models;
using SosuBot.Database;
using SosuBot.Helpers;
using SosuBot.Helpers.Comparers;
using SosuBot.Helpers.Scoring;
using SosuBot.Helpers.Types;
using SosuBot.Helpers.Types.Statistics;
using SosuBot.Services.Data.OsuApi;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace SosuBot.Services.BackgroundServices;

public sealed class ScoresObserverBackgroundService(
    ApiV2 osuApi,
    ILogger<ScoresObserverBackgroundService> logger,
    ITelegramBotClient botClient,
    BotContext database) : BackgroundService
{
    public static readonly ConcurrentBag<long> ObservedUsers = new();
    public static List<DailyStatistics> AllDailyStatistics = new();

    private static readonly ScoreEqualityComparer ScoreComparer = new();

    private readonly UserStatisticsCacheDatabase _userDatabase = new(osuApi);

    private static readonly string CacheDirectory =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache", "daily_statistics");

    private static readonly string CachePath =
        Path.Combine(CacheDirectory, "statistics.cache");

    private long _adminTelegramId;


    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Scores observer background service started");

        _adminTelegramId = (await database.OsuUsers.FirstAsync((m) => m.IsAdmin, cancellationToken: stoppingToken))
            .TelegramId;

        await AddPlayersToObserverList("uz");
        await AddPlayersToObserverList();

        await Task.WhenAll(ObserveScoresGetScores(stoppingToken), ObserveScores(stoppingToken));
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

        string? cursor = null;
        int counter = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                ScoresResponse? response =
                    await osuApi.Scores.GetScores(new() { CursorString = cursor, Ruleset = Ruleset.Osu });
                if (response == null)
                {
                    logger.LogWarning("GetScores() returned null");
                    continue;
                }

                Score[] uzScores = response.Scores!.Where(m => _userDatabase.ContainsUserStatistics(m.UserId!.Value))
                    .ToArray();
                foreach (var score in uzScores)
                {
                    UserStatistics? userStatistics = await _userDatabase.GetUserStatistics(score.UserId!.Value);
                    if (userStatistics == null)
                    {
                        logger.LogError($"User statistics is null for userId = {score.UserId!.Value}");
                        continue;
                    }

                    dailyStatistics.Scores.Add(score);

                    // ReSharper disable once SimplifyLinqExpressionUseAll
                    if (!dailyStatistics.ActiveUsers.Any(m => m.Id == userStatistics.User!.Id))
                    {
                        dailyStatistics.ActiveUsers.Add(userStatistics.User!);
                    }

                    // ReSharper disable once SimplifyLinqExpressionUseAll
                    if (!dailyStatistics.BeatmapsPlayed.Any(m => m == score.BeatmapId!.Value))
                    {
                        dailyStatistics.BeatmapsPlayed.Add(score.BeatmapId!.Value);
                    }
                }

                // New day => send statistics
                if (DateTime.UtcNow.Day != dailyStatistics.DayOfStatistic.Day)
                {
                    string sendText = await ScoreHelper.GetDailyStatisticsSendText(dailyStatistics, osuApi, logger);

                    await botClient.SendMessage(_adminTelegramId, sendText,
                        ParseMode.Html, linkPreviewOptions: true, cancellationToken: stoppingToken);

                    dailyStatistics = new DailyStatistics(CountryCode.Uzbekistan, DateTime.UtcNow);
                    AllDailyStatistics.Add(dailyStatistics);
                }

                // Save every timedelay*100 seconds
                if (counter % 100 == 0)
                {
                    await SaveDailyStatistics();
                }

                counter = (counter + 1) % int.MaxValue;
                cursor = response.CursorString;
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
                    GetUserScoresResponse? userBestScores =
                        await osuApi.Users.GetUserScores(userId, ScoreType.Best, new() { Limit = 50 });
                    if (userBestScores == null)
                    {
                        logger.LogWarning($"{userId} has no scores!");
                        continue;
                    }

                    // ReSharper disable once CanSimplifyDictionaryLookupWithTryGetValue
                    if (scores.ContainsKey(userId))
                    {
                        IEnumerable<Score> newScores =
                            userBestScores.Scores.Except(scores[userId].Scores, ScoreComparer);
                        foreach (Score score in newScores)
                        {
                            await botClient.SendMessage(_adminTelegramId,
                                $"<b>{score.User?.Username}</b> set a <b>{score.Pp}pp</b> {ScoreHelper.GetScoreUrlWrappedInString(score.Id!.Value, "score!")}",
                                ParseMode.Html, linkPreviewOptions: true, cancellationToken: stoppingToken!);
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

    /// <summary>
    /// Get the best players using some filter
    /// </summary>
    /// <param name="countryCode">If null, take from the global ranking</param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    private async Task<UserStatistics[]> GetBestPlayersFor(string? countryCode = null)
    {
        Rankings? rankings = await osuApi.Rankings.GetRanking(Ruleset.Osu, RankingType.Performance,
            new() { Country = countryCode, Filter = Filter.All });
        if (rankings == null)
        {
            logger.LogError($"Ranking is null. {countryCode}");
            throw new Exception("Ranking is null. See logs for details");
        }

        return rankings.Ranking!;
    }

    /// <summary>
    /// Add players to the <see cref="ObservedUsers"/>
    /// </summary>
    /// <param name="countryCode">If null, take from global ranking</param>
    /// <param name="count">Amount of players to add</param>
    private async Task AddPlayersToObserverList(string? countryCode = null, int count = 50)
    {
        UserStatistics[] bestPlayersStatistics = (await GetBestPlayersFor(countryCode)).Take(count).ToArray();
        foreach (UserStatistics playerStatistics in bestPlayersStatistics)
        {
            ObservedUsers.Add(playerStatistics.User!.Id.Value);
        }
    }

    private async Task SaveDailyStatistics()
    {
        if (!Directory.Exists(CacheDirectory))
        {
            Directory.CreateDirectory(CacheDirectory);
        }

        await File.WriteAllTextAsync(CachePath, JsonSerializer.Serialize(AllDailyStatistics));
    }

    private async Task LoadDailyStatistics()
    {
        if (!File.Exists(CachePath)) return;

        AllDailyStatistics = JsonSerializer.Deserialize<List<DailyStatistics>>(await File.ReadAllTextAsync(CachePath))!;
    }
}