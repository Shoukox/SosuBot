using System.Collections.Concurrent;
using System.Net;
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
using SosuBot.Extensions;
using SosuBot.Helpers.Comparers;
using SosuBot.Helpers.OutputText;
using SosuBot.Helpers.Types;
using SosuBot.Helpers.Types.Statistics;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Country = SosuBot.Helpers.Country;

namespace SosuBot.Services.BackgroundServices;

public sealed class ScoresObserverBackgroundService(
    ApiV2 osuApi,
    ITelegramBotClient botClient,
    BotContext database,
    ILogger<ScoresObserverBackgroundService> logger) : BackgroundService
{
    public static readonly ConcurrentBag<long> ObservedUsers = new();
    public static List<DailyStatistics> AllDailyStatistics = new();

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
            _adminTelegramId = (await database.OsuUsers.FirstAsync(m => m.IsAdmin, cancellationToken: stoppingToken))
                .TelegramId;
            await AddPlayersToObserverList("uz");
            await AddPlayersToObserverList();

            await Task.WhenAll(
                ObserveScoresGetScores(stoppingToken),
                ObserveScores(stoppingToken)
            );
        }
        catch (OperationCanceledException e)
        {
            logger.LogError(e, "Operation cancelled");
        }
    }

    private async Task ObserveScoresGetScores(CancellationToken stoppingToken)
    {
        await LoadDailyStatistics();
        await _userDatabase.CacheIfNeeded();

        DailyStatistics dailyStatistics;
        if (AllDailyStatistics.Count > 0 && AllDailyStatistics.Last().DayOfStatistic.Day ==
            DateTime.UtcNow.ChangeTimezone(Country.Uzbekistan).Day)
        {
            dailyStatistics = AllDailyStatistics.Last();
        }
        else
        {
            dailyStatistics =
                new DailyStatistics(CountryCode.Uzbekistan, DateTime.UtcNow.ChangeTimezone(Country.Uzbekistan));
            AllDailyStatistics.Add(dailyStatistics);
        }

        string? getStdScoresCursor = null;
        string? getTaikoScoresCursor = null;
        string? getFruitsScoresCursor = null;
        string? getManiaScoresCursor = null;
        var counter = 0;
        while (!stoppingToken.IsCancellationRequested)
            try
            {
                var getStdScoresResponseTask = osuApi.Scores.GetScores(new ScoresQueryParameters
                    { CursorString = getStdScoresCursor, Ruleset = Ruleset.Osu });
                var getTaikoScoresResponseTask = osuApi.Scores.GetScores(new ScoresQueryParameters
                    { CursorString = getTaikoScoresCursor, Ruleset = Ruleset.Taiko });
                var getFruitsScoresResponseTask = osuApi.Scores.GetScores(new ScoresQueryParameters
                    { CursorString = getFruitsScoresCursor, Ruleset = Ruleset.Fruits });
                var getManiaScoresResponseTask = osuApi.Scores.GetScores(new ScoresQueryParameters
                    { CursorString = getManiaScoresCursor, Ruleset = Ruleset.Mania });

                await Task.WhenAll(getStdScoresResponseTask, getTaikoScoresResponseTask, getFruitsScoresResponseTask,
                    getManiaScoresResponseTask);

                var getStdScoresResponse = getStdScoresResponseTask.Result;
                var getTaikoScoresResponse = getTaikoScoresResponseTask.Result;
                var getFruitsScoresResponse = getFruitsScoresResponseTask.Result;
                var getManiaScoresResponse = getManiaScoresResponseTask.Result;

                if (getStdScoresResponse == null)
                {
                    logger.LogWarning("getStdScoresResponse returned null");
                    continue;
                }

                if (getTaikoScoresResponse == null)
                {
                    logger.LogWarning("getTaikoScoresResponse returned null");
                    continue;
                }

                if (getFruitsScoresResponse == null)
                {
                    logger.LogWarning("getFruitsScoresResponse returned null");
                    continue;
                }

                if (getManiaScoresResponse == null)
                {
                    logger.LogWarning("getManiaScoresResponse returned null");
                    continue;
                }

                var allOsuScores = getStdScoresResponse.Scores!
                    .Select(m => m with { Mode = Ruleset.Osu, ModeInt = (int)Playmode.Osu })
                    .Concat(getTaikoScoresResponse.Scores!.Select(m =>
                        m with { Mode = Ruleset.Taiko, ModeInt = (int)Playmode.Taiko }))
                    .Concat(getFruitsScoresResponse.Scores!.Select(m =>
                        m with { Mode = Ruleset.Fruits, ModeInt = (int)Playmode.Catch }))
                    .Concat(getManiaScoresResponse.Scores!.Select(m =>
                        m with { Mode = Ruleset.Mania, ModeInt = (int)Playmode.Mania }));

                // Scores only from UZ and only from today
                var tashkentToday = DateTime.UtcNow.ChangeTimezone(Country.Uzbekistan).Date;
                var uzScores = allOsuScores.Where(m =>
                {
                    bool scoreDateIsOk = m.EndedAt!.Value.ChangeTimezone(Country.Uzbekistan) >= tashkentToday;
                    bool isUzPlayer = _userDatabase.ContainsUserStatistics(m.UserId!.Value);
                    return scoreDateIsOk && isUzPlayer;
                }).ToArray();
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
                if (DateTime.UtcNow.ChangeTimezone(Country.Uzbekistan).Day != dailyStatistics.DayOfStatistic.Day)
                {
                    try
                    {
                        for (var i = 0; i <= 3; i++)
                        {
                            var sendText =
                                await ScoreHelper.GetDailyStatisticsSendText((Playmode)i, dailyStatistics, osuApi);
                            await botClient.SendMessage(_adminTelegramId, sendText,
                                ParseMode.Html, linkPreviewOptions: true, cancellationToken: stoppingToken);
                            await Task.Delay(1000, stoppingToken);
                        }
                    }
                    catch (Exception e)
                    {
                        logger.LogError(e, "Error while sending final daily statistics");
                    }

                    dailyStatistics = new DailyStatistics(CountryCode.Uzbekistan,
                        DateTime.UtcNow.ChangeTimezone(Country.Uzbekistan));
                    AllDailyStatistics.Add(dailyStatistics);
                }

                // Save every timedelay*100 seconds
                if (counter % 100 == 0) await SaveDailyStatistics();

                counter = (counter + 1) % int.MaxValue;

                getStdScoresCursor = getStdScoresResponse.CursorString;
                getTaikoScoresCursor = getTaikoScoresResponse.CursorString;
                getFruitsScoresCursor = getFruitsScoresResponse.CursorString;
                getManiaScoresCursor = getManiaScoresResponse.CursorString;
                await Task.Delay(7_000, stoppingToken);
            }
            catch (OperationCanceledException e)
            {
                logger.LogError(e, "Operation cancelled");
                return;
            }
            catch (HttpRequestException httpRequestException)
            {
                if (httpRequestException.StatusCode != null && ((int)httpRequestException.StatusCode >= 500 || httpRequestException.StatusCode == HttpStatusCode.RequestTimeout))
                {
                    int waitMs = 10_000;
                    logger.LogWarning($"OsuApi: status code {httpRequestException.StatusCode}. Waiting {waitMs}ms...");
                    await Task.Delay(waitMs, stoppingToken);
                }
            }
            catch (Exception e)
            {
                logger.LogError(e, "[ObserveScoresGetScores] Unexpected exception");
            }
    }

    private async Task ObserveScores(CancellationToken stoppingToken)
    {
        Dictionary<int, GetUserScoresResponse> scores = new();
        while (!stoppingToken.IsCancellationRequested)
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
                                ParseMode.Html, linkPreviewOptions: true, cancellationToken: stoppingToken);
                            await Task.Delay(1000, stoppingToken);
                        }
                    }

                    scores[userId] = userBestScores;
                    await Task.Delay(1000, stoppingToken);
                }
            }
            catch (OperationCanceledException e)
            {
                logger.LogError(e, "Operation cancelled");
                return;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Unexpected exception");
            }

        logger.LogWarning("Finished its work");
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