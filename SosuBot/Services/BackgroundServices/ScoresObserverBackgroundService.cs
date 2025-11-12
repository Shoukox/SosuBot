using System.Buffers.Text;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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

public sealed class ScoresObserverBackgroundService(IServiceProvider serviceProvider) : BackgroundService
{
    private readonly ITelegramBotClient _botClient = serviceProvider.GetRequiredService<ITelegramBotClient>();
    private readonly BotContext _database = serviceProvider.GetRequiredService<BotContext>();
    private readonly ApiV2 _osuApi = serviceProvider.GetRequiredService<ApiV2>();

    private readonly ILogger<ScoresObserverBackgroundService> _logger =
        serviceProvider.GetRequiredService<ILogger<ScoresObserverBackgroundService>>();

    private static readonly SemaphoreSlim Semaphore = new SemaphoreSlim(1, 1);
    public static readonly ConcurrentBag<int> ObservedUsers = new();
    public static List<DailyStatistics> AllDailyStatistics = new();

    private static readonly ScoreEqualityComparer ScoreComparer = new();

    private static readonly string CacheDirectory =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache", "daily_statistics");

    private static readonly string CachePath =
        Path.Combine(CacheDirectory, "statistics.cache");

    private UserStatisticsCacheDatabase _userDatabase = null!; // initialized in ExecuteAsync
    private long _adminTelegramId;

    private async Task SetupObserverList()
    {
        await AddPlayersToObserverListFromSpecificCountryLeaderboard("uz");
        await AddPlayersToObserverListFromSpecificCountryLeaderboard();
        
        var chatsWithTrackedPlayers = _database.TelegramChats.Where(m => m.TrackedPlayers != null);
        _logger.LogInformation($"Found {chatsWithTrackedPlayers.Count()} chats with tracked players");
        
        await Task.WhenAll(chatsWithTrackedPlayers.Select(m => AddPlayersToObserverList(m.TrackedPlayers!.ToArray())));
        _logger.LogInformation($"Successfully added tracked players to the {nameof(ScoresObserverBackgroundService)}");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _userDatabase = new(_osuApi);
        _logger.LogInformation("Scores observer background service started");

        try
        {
            _adminTelegramId = (await _database.OsuUsers.FirstAsync(m => m.IsAdmin == true))
                .TelegramId;
            
            await SetupObserverList();

            await Task.WhenAll(
                ObserveScoresGetScores(stoppingToken),
                ObserveScores(stoppingToken)
            );
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Operation cancelled");
        }

        _logger.LogInformation("Finished its work");
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

        //string cursor = Convert.ToBase64String("{\"id\":5737168425}"u8.ToArray());
        string? getStdScoresCursor = null;
        string? getTaikoScoresCursor = null;
        string? getFruitsScoresCursor = null;
        string? getManiaScoresCursor = null;
        var counter = 0;
        while (!stoppingToken.IsCancellationRequested)
            try
            {
                var getStdScoresResponseTask = _osuApi.Scores.GetScores(new ScoresQueryParameters
                    { CursorString = getStdScoresCursor, Ruleset = Ruleset.Osu });
                var getTaikoScoresResponseTask = _osuApi.Scores.GetScores(new ScoresQueryParameters
                    { CursorString = getTaikoScoresCursor, Ruleset = Ruleset.Taiko });
                var getFruitsScoresResponseTask = _osuApi.Scores.GetScores(new ScoresQueryParameters
                    { CursorString = getFruitsScoresCursor, Ruleset = Ruleset.Fruits });
                var getManiaScoresResponseTask = _osuApi.Scores.GetScores(new ScoresQueryParameters
                    { CursorString = getManiaScoresCursor, Ruleset = Ruleset.Mania });
                
                await Task.WhenAll(getStdScoresResponseTask, getTaikoScoresResponseTask, getFruitsScoresResponseTask,
                    getManiaScoresResponseTask);

                var getStdScoresResponse = getStdScoresResponseTask.Result;
                var getTaikoScoresResponse = getTaikoScoresResponseTask.Result;
                var getFruitsScoresResponse = getFruitsScoresResponseTask.Result;
                var getManiaScoresResponse = getManiaScoresResponseTask.Result;
                
                if (getStdScoresResponse == null)
                {
                    _logger.LogWarning("getStdScoresResponse returned null");
                    continue;
                }

                if (getTaikoScoresResponse == null)
                {
                    _logger.LogWarning("getTaikoScoresResponse returned null");
                    continue;
                }

                if (getFruitsScoresResponse == null)
                {
                    _logger.LogWarning("getFruitsScoresResponse returned null");
                    continue;
                }

                if (getManiaScoresResponse == null)
                {
                    _logger.LogWarning("getManiaScoresResponse returned null");
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
                    var scoreDateIsOk = m.EndedAt!.Value.ChangeTimezone(Country.Uzbekistan) >= tashkentToday;
                    var isUzPlayer = _userDatabase.ContainsUserStatistics(m.UserId!.Value);
                    return scoreDateIsOk && isUzPlayer;
                }).ToArray();
                foreach (var score in uzScores)
                {
                    var userStatistics = await _userDatabase.GetUserStatistics(score.UserId!.Value);
                    if (userStatistics == null)
                    {
                        _logger.LogError($"User statistics is null for userId = {score.UserId!.Value}");
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
                                await ScoreHelper.GetDailyStatisticsSendText((Playmode)i, dailyStatistics, _osuApi);
                            await _botClient.SendMessage(_adminTelegramId, sendText,
                                ParseMode.Html, linkPreviewOptions: true);
                            await Task.Delay(1000);
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Error while sending final daily statistics");
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
                
                _logger.LogInformation($"Current cursor for std: {getStdScoresCursor}");
                _logger.LogInformation($"Current cursor for taiko: {getTaikoScoresCursor}");
                _logger.LogInformation($"Current cursor for fruits: {getFruitsScoresCursor}");
                _logger.LogInformation($"Current cursor for mania: {getManiaScoresCursor}");
                await Task.Delay(7000);
            }
            catch (HttpRequestException httpRequestException)
            {
                var waitMs = 10_000;
                _logger.LogWarning($"[{nameof(ScoresObserverBackgroundService)}]: status code {httpRequestException.StatusCode}. Waiting {waitMs}ms...");
                await Task.Delay(waitMs);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "[ObserveScoresGetScores] Unexpected exception");
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
                        await _osuApi.Users.GetUserScores(userId, ScoreType.Best,
                            new GetUserScoreQueryParameters { Limit = 50 });
                    if (userBestScores == null)
                    {
                        _logger.LogWarning($"{userId} has no scores!");
                        continue;
                    }

                    // ReSharper disable once CanSimplifyDictionaryLookupWithTryGetValue
                    if (scores.ContainsKey(userId))
                    {
                        _ = Task.Run(async () =>
                        {
                            var newScores =
                                userBestScores.Scores.Except(scores[userId].Scores, ScoreComparer);
                            
                            foreach (var score in newScores)
                            {
                                // Send it to the admin
                                await _botClient.SendMessage(_adminTelegramId,
                                    $"<b>{score.User?.Username}</b> set a <b>{score.Pp}pp</b> {ScoreHelper.GetScoreUrlWrappedInString(score.Id!.Value, "score!")}",
                                    ParseMode.Html, linkPreviewOptions: true);
                                await Task.Delay(1000);

                                
                                // Send it to all chats tracking 
                                var chatsToSend = _database.TelegramChats.Where(m => m.TrackedPlayers != null && m.TrackedPlayers.Contains(userId));
                                foreach (var chat in chatsToSend)
                                {
                                    await _botClient.SendMessage(chat.ChatId,
                                        $"<b>{score.User?.Username}</b> set a <b>{score.Pp}pp</b> {ScoreHelper.GetScoreUrlWrappedInString(score.Id!.Value, "score!")}",
                                        ParseMode.Html, linkPreviewOptions: true);
                                    await Task.Delay(1000);
                                }
                            }
                        }).ConfigureAwait(false);
                    }

                    scores[userId] = userBestScores;
                    await Task.Delay(5000);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Unexpected exception");
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
        var rankings = await _osuApi.Rankings.GetRanking(Ruleset.Osu, RankingType.Performance,
            new GetRankingQueryParameters { Country = countryCode, Filter = Filter.All });
        if (rankings == null)
        {
            _logger.LogError($"Ranking is null. {countryCode}");
            throw new Exception("Ranking is null. See logs for details");
        }

        return rankings.Ranking!;
    }

    /// <summary>
    ///     Add players to the <see cref="ObservedUsers" /> from a specific country leaderboard
    /// </summary>
    /// <param name="countryCode">If null, take from global ranking</param>
    /// <param name="count">Amount of players to add</param>
    private async Task AddPlayersToObserverListFromSpecificCountryLeaderboard(string? countryCode = null,
        int count = 50)
    {
        var bestPlayersStatistics = (await GetBestPlayersFor(countryCode)).Take(count).ToArray();
        foreach (var playerStatistics in bestPlayersStatistics) ObservedUsers.Add(playerStatistics.User!.Id.Value);
    }

    /// <summary>
    ///     Add players to the <see cref="ObservedUsers" /> if they are not there
    /// </summary>
    /// <param name="countryCode">If null, take from global ranking</param>
    /// <param name="count">Amount of players to add</param>
    public static async Task AddPlayersToObserverList(int[] playerIds)
    {
        try
        {
            await Semaphore.WaitAsync();
            foreach (var osuUserId in playerIds)
            {
                if (!ObservedUsers.Contains(osuUserId))
                {
                    ObservedUsers.Add(osuUserId);
                }
            }
        }
        finally
        {
            Semaphore.Release();
        }
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