using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
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
using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.Helpers.Comparers;
using SosuBot.Helpers.OutputText;
using SosuBot.Helpers.Types;
using SosuBot.Helpers.Types.Statistics;
using System.Collections.Concurrent;
using System.Text.Json;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Country = SosuBot.Helpers.Country;

namespace SosuBot.Services.BackgroundServices;

public sealed class ScoresObserverBackgroundService(IServiceProvider serviceProvider) : BackgroundService
{
    private ITelegramBotClient _botClient = null!;
    private BotContext _database = null!;
    private ApiV2 _osuApi = null!;
    private ScoreHelper _scoreHelper = serviceProvider.GetRequiredService<ScoreHelper>();
    private ILogger<ScoresObserverBackgroundService> _logger = serviceProvider.GetRequiredService<ILogger<ScoresObserverBackgroundService>>();

    private static readonly SemaphoreSlim Semaphore = new SemaphoreSlim(1, 1);
    public static ConcurrentBag<int> ObservedUsers = new();

    private static readonly ScoreEqualityComparer ScoreComparer = new();

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
        using var scope = serviceProvider.CreateScope();
        _botClient = scope.ServiceProvider.GetRequiredService<ITelegramBotClient>();
        _database = scope.ServiceProvider.GetRequiredService<BotContext>();
        _osuApi = scope.ServiceProvider.GetRequiredService<ApiV2>();

        _userDatabase = new(_osuApi);
        _logger.LogInformation("Scores observer background service started");

        try
        {
            _adminTelegramId = (await _database.OsuUsers.FirstAsync(m => m.IsAdmin == true))
                .TelegramId;

            await SetupObserverList();
            await LoadDailyStatistics();

            await Task.WhenAll(
                ObserveScores(stoppingToken),
                ObserveScoresGetScores(stoppingToken)
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
        DailyStatistics dailyStatistics;
        if (_database.DailyStatistics.Count() > 0 && _database.DailyStatistics.OrderBy(m => m.Id).Last().DayOfStatistic.Day == DateTime.UtcNow.ChangeTimezone(Country.Uzbekistan).Day)
        {
            dailyStatistics = _database.DailyStatistics.OrderBy(m => m.Id).Last();
        }
        else
        {
            dailyStatistics = new DailyStatistics()
            {
                CountryCode = CountryCode.Uzbekistan,
                DayOfStatistic = DateTime.UtcNow.ChangeTimezone(Country.Uzbekistan)
            };
            _database.DailyStatistics.Add(dailyStatistics);
            _database.SaveChanges();
        }

        //string cursor = Convert.ToBase64String("{\"id\":5737168425}"u8.ToArray());
        string? getStdScoresCursor = null;
        string? getTaikoScoresCursor = null;
        string? getFruitsScoresCursor = null;
        string? getManiaScoresCursor = null;

        ulong counter = 0;
        while (!stoppingToken.IsCancellationRequested)
            try
            {
                ScoresResponse? getStdScoresResponse = await _osuApi.Scores.GetScores(new() { CursorString = getStdScoresCursor, Ruleset = Ruleset.Osu });

                // get taiko scores every 4th iteration (for delay)
                ScoresResponse? getTaikoScoresResponse = null;
                if (counter % 8 == 0)
                {
                    getTaikoScoresResponse = await _osuApi.Scores.GetScores(new ScoresQueryParameters { CursorString = getTaikoScoresCursor, Ruleset = Ruleset.Taiko });
                }

                // get fruits scores every 4th iteration (for delay)
                ScoresResponse? getFruitsScoresResponse = null;
                if (counter % 12 == 0)
                {
                    getFruitsScoresResponse = await _osuApi.Scores.GetScores(new ScoresQueryParameters { CursorString = getFruitsScoresCursor, Ruleset = Ruleset.Fruits });
                }

                // get mania scores every 4th iteration (for delay)
                ScoresResponse? getManiaScoresResponse = null;
                if (counter % 4 == 0)
                {
                    getManiaScoresResponse = await _osuApi.Scores.GetScores(new ScoresQueryParameters { CursorString = getManiaScoresCursor, Ruleset = Ruleset.Mania });
                }

                if (getStdScoresResponse == null && getTaikoScoresResponse == null && getFruitsScoresResponse == null && getManiaScoresResponse == null)
                {
                    _logger.LogWarning($"GetScores returned null: stdNull={getStdScoresResponse == null} taikoNull={getTaikoScoresResponse == null} fruitsNull={getFruitsScoresResponse == null} maniaNull={getManiaScoresResponse == null}");
                    _logger.LogWarning("Waiting 60 seconds before retrying...");
                    await Task.Delay(60_000);
                    continue;
                }

                var allOsuScores = getStdScoresResponse?.Scores?
                    .Select(m => m with { Mode = Ruleset.Osu, ModeInt = (int)Playmode.Osu })
                    .Concat(getTaikoScoresResponse?.Scores?.Select(m =>
                        m with { Mode = Ruleset.Taiko, ModeInt = (int)Playmode.Taiko }) ?? [])
                    .Concat(getFruitsScoresResponse?.Scores?.Select(m =>
                        m with { Mode = Ruleset.Fruits, ModeInt = (int)Playmode.Catch }) ?? [])
                    .Concat(getManiaScoresResponse?.Scores?.Select(m =>
                        m with { Mode = Ruleset.Mania, ModeInt = (int)Playmode.Mania }) ?? []) ?? [];

                if (!allOsuScores.Any())
                {
                    _logger.LogWarning("No scores retrieved from GetScores. Waiting 20 seconds before retrying...");
                    await Task.Delay(20_000);
                    continue;
                }

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

                    dailyStatistics.Scores.Add(new ScoreEntity() { ScoreId = score.Id!.Value, ScoreJson = score });

                    if (!dailyStatistics.ActiveUsers.Any(m => m.UserId == userStatistics.User!.Id))
                    {
                        if (_database.UserEntity.Find(userStatistics.User!.Id) is { } foundUserEntity)
                        {
                            dailyStatistics.ActiveUsers.Add(foundUserEntity);
                        }
                        else
                        {
                            dailyStatistics.ActiveUsers.Add(new UserEntity() { UserId = userStatistics.User!.Id!.Value, UserJson = userStatistics.User! });
                        }
                    }

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
                                await _scoreHelper.GetDailyStatisticsSendText((Playmode)i, dailyStatistics, _osuApi);
                            await _botClient.SendMessage(_adminTelegramId, sendText,
                                ParseMode.Html, linkPreviewOptions: true);
                            await Task.Delay(1000);
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Error while sending final daily statistics");
                    }

                    dailyStatistics = new DailyStatistics() { CountryCode = CountryCode.Uzbekistan, DayOfStatistic = DateTime.UtcNow.ChangeTimezone(Country.Uzbekistan) };
                    _database.DailyStatistics.Add(dailyStatistics);
                    _database.SaveChanges();
                }

                if (getStdScoresResponse != null) getStdScoresCursor = getStdScoresResponse.CursorString;
                if (getTaikoScoresResponse != null) getTaikoScoresCursor = getTaikoScoresResponse.CursorString;
                if (getFruitsScoresResponse != null) getFruitsScoresCursor = getFruitsScoresResponse.CursorString;
                if (getManiaScoresResponse != null) getManiaScoresCursor = getManiaScoresResponse.CursorString;

                if (getStdScoresResponse?.Scores?.Length >= 1000 || getTaikoScoresResponse?.Scores?.Length >= 1000 || getFruitsScoresResponse?.Scores?.Length >= 1000 || getManiaScoresResponse?.Scores?.Length >= 1000)
                {
                    _logger.LogWarning($"GetScores returned 1000+ scores in one of the modes. std={getStdScoresResponse?.Scores?.Length} taiko={getTaikoScoresResponse?.Scores?.Length} fruits={getFruitsScoresResponse?.Scores?.Length} mania={getManiaScoresResponse?.Scores?.Length}");
                }

                int stdScoresCount = Math.Max(1, getStdScoresResponse?.Scores?.Length ?? 1);
                int delay = 3000 + 1000 * (1000 / stdScoresCount); //3sec + 1*n seconds
                int clampedDelay = Math.Clamp(delay, 3000, 55_000);
                _logger.LogInformation($"Processed {stdScoresCount} std scores. Next GetScores in {clampedDelay}ms.");
                _database.SaveChanges();

                await Task.Delay(clampedDelay);
                counter++;
            }
            catch (HttpRequestException httpRequestException)
            {
                var waitMs = 20_000;
                _logger.LogWarning($"[{nameof(ScoresObserverBackgroundService)}]: status code {httpRequestException.StatusCode}. Message: {httpRequestException.Message}. Waiting {waitMs}ms...");
                await Task.Delay(waitMs);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "[ObserveScoresGetScores] Unexpected exception");
            }
    }

    private async Task ObserveScores(CancellationToken stoppingToken)
    {
        int scoresLimit = 50;
        Dictionary<int, GetUserScoresResponse> scores = new();
        while (!stoppingToken.IsCancellationRequested)
            try
            {
                foreach (int userId in ObservedUsers)
                {
                    var userBestScores =
                        await _osuApi.Users.GetUserScores(userId, ScoreType.Best,
                            new GetUserScoreQueryParameters { Limit = scoresLimit });
                    if (userBestScores == null)
                    {
                        _logger.LogWarning($"{userId} has no scores!");
                        continue;
                    }

                    if (scores.ContainsKey(userId) && !scores[userId].Scores.Any(m => m.ModeInt != userBestScores.Scores.FirstOrDefault()?.ModeInt))
                    {
                        Score[] newScores =
                            userBestScores.Scores.Except(scores[userId].Scores, ScoreComparer).ToArray();

                        // if new scores amount equals to the limit, then probably the user has switched his default game mode
                        if (newScores.Length != scoresLimit)
                        {
                            _ = Task.Run(async () =>
                            {
                                foreach (var score in newScores)
                                {
                                    try
                                    {
                                        int waitMs = 1000 + Random.Shared.Next(500, 1500);
                                        // Send it to the admin
                                        await _botClient.SendMessage(_adminTelegramId,
                                            $"<b>{score.User?.Username}</b> set a <b>{score.Pp}pp</b> {_scoreHelper.GetScoreUrlWrappedInString(score.Id!.Value, "score!")}",
                                            ParseMode.Html, linkPreviewOptions: true);
                                        await Task.Delay(waitMs);


                                        // Send it to all chats tracking 
                                        var chatsToSend = _database.TelegramChats.Where(m => m.TrackedPlayers != null && m.TrackedPlayers.Contains(userId));
                                        foreach (var chat in chatsToSend)
                                        {
                                            await _botClient.SendMessage(chat.ChatId,
                                                $"<b>{score.User?.Username}</b> set a <b>{score.Pp}pp</b> {_scoreHelper.GetScoreUrlWrappedInString(score.Id!.Value, "score!")}",
                                                ParseMode.Html, linkPreviewOptions: true);
                                            await Task.Delay(waitMs);
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        _logger.LogError(e, "Error while sending new score notification");
                                    }
                                }
                            }).ConfigureAwait(false);
                        }
                    }

                    scores[userId] = userBestScores;
                    await Task.Delay(5000);
                }
            }
            catch (HttpRequestException httpRequestException)
            {
                var waitMs = 10_000;
                _logger.LogWarning($"[{nameof(ScoresObserverBackgroundService)}]: status code {httpRequestException.StatusCode}. Message: {httpRequestException.Message}. Waiting {waitMs}ms...");
                await Task.Delay(waitMs);
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

    public static async Task RemovePlayersFromObserverList(IEnumerable<int> playerIds)
    {
        try
        {
            await Semaphore.WaitAsync();
            ObservedUsers = new ConcurrentBag<int>(ObservedUsers.Except(playerIds));
        }
        finally
        {
            Semaphore.Release();
        }
    }

    public static List<DailyStatistics> AllDailyStatistics = new();

    private static readonly string CacheDirectory =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache", "daily_statistics");

    private static readonly string CachePath =
        Path.Combine(CacheDirectory, "statistics.cache");

    private async Task SaveDailyStatistics()
    {
        if (!Directory.Exists(CacheDirectory)) Directory.CreateDirectory(CacheDirectory);

        await File.WriteAllTextAsync(CachePath, JsonSerializer.Serialize(AllDailyStatistics));
    }

    private async Task LoadDailyStatistics()
    {
        if (!File.Exists(CachePath)) return;

        var list = JsonSerializer.Deserialize<List<DailyStats>>(await File.ReadAllTextAsync(CachePath));
        AllDailyStatistics = list!.Select(m => new DailyStatistics()
        {
            CountryCode = m.CountryCode,
            DayOfStatistic = m.DayOfStatistic,
            ActiveUsers = m.ActiveUsers.Select(m => new UserEntity() { UserId = m.Id!.Value, UserJson = m }).ToList(),
            Scores = m.Scores.Select(m => new ScoreEntity() { ScoreId = m.Id!.Value, ScoreJson = m }).ToList(),
            BeatmapsPlayed = m.BeatmapsPlayed
        }).ToList();
    }
}