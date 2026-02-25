using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OsuApi.BanchoV2;
using OsuApi.BanchoV2.Clients.Rankings.HttpIO;
using OsuApi.BanchoV2.Clients.Scores.HttpIO;
using OsuApi.BanchoV2.Clients.Users.HttpIO;
using OsuApi.BanchoV2.Models;
using OsuApi.BanchoV2.Users.Models;
using SosuBot.Database;
using SosuBot.Database.Models;
using SosuBot.ScoresObserver.Comparers;
using SosuBot.ScoresObserver.Extensions;
using SosuBot.ScoresObserver.Models;
using System.Collections.Concurrent;
using System.Text.Json;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Country = SosuBot.ScoresObserver.Models.Country;

namespace SosuBot.ScoresObserver.Services;

public sealed class ScoresObserverBackgroundService(IServiceProvider serviceProvider) : BackgroundService
{
    private readonly ITelegramBotClient _botClient = serviceProvider.GetRequiredService<ITelegramBotClient>();
    private readonly BotContext _database = serviceProvider.GetRequiredService<BotContext>();
    private readonly BanchoApiV2 _osuApi = serviceProvider.GetRequiredService<BanchoApiV2>();
    private readonly ILogger<ScoresObserverBackgroundService> _logger = serviceProvider.GetRequiredService<ILogger<ScoresObserverBackgroundService>>();

    private static readonly SemaphoreSlim Semaphore = new(1, 1);
    public static ConcurrentBag<int> ObservedUsers = new();

    private static readonly ScoreEqualityComparer ScoreComparer = new();

    private UserStatisticsCacheDatabase _userDatabase = null!; // initialized in ExecuteAsync
    private long _adminTelegramId;

    private async Task SetupObserverList()
    {
        await AddPlayersToObserverListFromSpecificCountryLeaderboard("uz");
        await AddPlayersToObserverListFromSpecificCountryLeaderboard();

        var chatsWithTrackedPlayers = _database.TelegramChats.Where(m => m.TrackedPlayers != null);
        _logger.LogInformation("Found {ChatsCount} chats with tracked players", chatsWithTrackedPlayers.Count());

        await Task.WhenAll(chatsWithTrackedPlayers.Select(m => AddPlayersToObserverList(m.TrackedPlayers!.ToArray())));
        _logger.LogInformation("Successfully added tracked players to {ServiceName}", nameof(ScoresObserverBackgroundService));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _userDatabase = new(_osuApi);
        _logger.LogInformation("Scores observer background service started");

        try
        {
            _adminTelegramId = (await _database.OsuUsers.FirstAsync(m => m.IsAdmin, stoppingToken)).TelegramId;

            await SetupObserverList();

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
        await LoadDailyStatistics();
        await _userDatabase.CacheIfNeeded(stoppingToken);

        DailyStatistics dailyStatistics;
        if (_database.DailyStatistics.Count() > 0)
        {
            var lastDailyStatistics = _database.DailyStatistics.OrderBy(m => m.Id).Last();
            if (lastDailyStatistics.DayOfStatistic.Day == DateTime.UtcNow.ChangeTimezone(Country.Uzbekistan).DateTime.Day)
            {
                dailyStatistics = lastDailyStatistics;
            }
            else
            {
                dailyStatistics = new DailyStatistics
                {
                    CountryCode = CountryCode.Uzbekistan,
                    DayOfStatistic = DateTime.UtcNow.ChangeTimezone(Country.Uzbekistan).DateTime
                };
                _database.DailyStatistics.Add(dailyStatistics);
                _database.SaveChanges();
            }
        }
        else
        {
            dailyStatistics = new DailyStatistics
            {
                CountryCode = CountryCode.Uzbekistan,
                DayOfStatistic = DateTime.UtcNow.ChangeTimezone(Country.Uzbekistan).DateTime
            };
            _database.DailyStatistics.Add(dailyStatistics);
            _database.SaveChanges();
        }

        string? getStdScoresCursor = null;
        string? getTaikoScoresCursor = null;
        string? getFruitsScoresCursor = null;
        string? getManiaScoresCursor = null;

        ulong counter = 0;
        while (!stoppingToken.IsCancellationRequested)
            try
            {
                ScoresResponse? getStdScoresResponse = await _osuApi.Scores.GetScores(new() { CursorString = getStdScoresCursor, Ruleset = Ruleset.Osu });

                ScoresResponse? getTaikoScoresResponse = null;
                if (counter % 8 == 0)
                {
                    getTaikoScoresResponse = await _osuApi.Scores.GetScores(new ScoresQueryParameters { CursorString = getTaikoScoresCursor, Ruleset = Ruleset.Taiko });
                }

                ScoresResponse? getFruitsScoresResponse = null;
                if (counter % 12 == 0)
                {
                    getFruitsScoresResponse = await _osuApi.Scores.GetScores(new ScoresQueryParameters { CursorString = getFruitsScoresCursor, Ruleset = Ruleset.Fruits });
                }

                ScoresResponse? getManiaScoresResponse = null;
                if (counter % 4 == 0)
                {
                    getManiaScoresResponse = await _osuApi.Scores.GetScores(new ScoresQueryParameters { CursorString = getManiaScoresCursor, Ruleset = Ruleset.Mania });
                }

                if (getStdScoresResponse == null && getTaikoScoresResponse == null && getFruitsScoresResponse == null && getManiaScoresResponse == null)
                {
                    _logger.LogWarning("GetScores returned null for all modes. Waiting 60 seconds before retrying...");
                    await Task.Delay(60_000, stoppingToken);
                    continue;
                }

                var allOsuScores = getStdScoresResponse?.Scores?
                    .Select(m => m with { Mode = Ruleset.Osu, ModeInt = (int)Playmode.Osu })
                    .Concat(getTaikoScoresResponse?.Scores?.Select(m => m with { Mode = Ruleset.Taiko, ModeInt = (int)Playmode.Taiko }) ?? [])
                    .Concat(getFruitsScoresResponse?.Scores?.Select(m => m with { Mode = Ruleset.Fruits, ModeInt = (int)Playmode.Catch }) ?? [])
                    .Concat(getManiaScoresResponse?.Scores?.Select(m => m with { Mode = Ruleset.Mania, ModeInt = (int)Playmode.Mania }) ?? []) ?? [];

                if (!allOsuScores.Any())
                {
                    _logger.LogWarning("No scores retrieved from GetScores. Waiting 20 seconds before retrying...");
                    await Task.Delay(20_000, stoppingToken);
                    continue;
                }

                var tashkentToday = DateTime.UtcNow.ChangeTimezone(Country.Uzbekistan).Date;
                var uzScores = allOsuScores.Where(m =>
                {
                    var scoreDateIsOk = m.EndedAt!.Value.ChangeTimezone(Country.Uzbekistan) >= tashkentToday;
                    var isUzPlayer = _userDatabase.ContainsUserStatistics(m.UserId!.Value);
                    return scoreDateIsOk && isUzPlayer;
                }).ToArray();

                foreach (var score in uzScores)
                {
                    var userStatistics = await _userDatabase.GetUserStatistics(score.UserId!.Value, stoppingToken);
                    if (userStatistics?.User == null)
                    {
                        _logger.LogError("User statistics is null for userId = {UserId}", score.UserId!.Value);
                        continue;
                    }

                    if (!dailyStatistics.Scores.Any(m => m.ScoreId == score.Id!.Value))
                    {
                        dailyStatistics.Scores.Add(new ScoreEntity { ScoreId = score.Id!.Value, ScoreJson = score });
                    }

                    if (!dailyStatistics.ActiveUsers.Any(m => m.UserId == userStatistics.User.Id))
                    {
                        if (_database.UserEntity.Find(userStatistics.User.Id) is { } foundUserEntity)
                        {
                            dailyStatistics.ActiveUsers.Add(foundUserEntity);
                        }
                        else
                        {
                            dailyStatistics.ActiveUsers.Add(new UserEntity { UserId = userStatistics.User.Id!.Value, UserJson = userStatistics.User });
                        }
                    }

                    if (!dailyStatistics.BeatmapsPlayed.Any(m => m == score.BeatmapId!.Value))
                    {
                        dailyStatistics.BeatmapsPlayed.Add(score.BeatmapId!.Value);
                    }
                }

                if (DateTime.UtcNow.ChangeTimezone(Country.Uzbekistan).DateTime.Day != dailyStatistics.DayOfStatistic.Day)
                {
                    try
                    {
                        for (var i = 0; i <= 3; i++)
                        {
                            var sendText = BuildDailyStatisticsSendText((Playmode)i, dailyStatistics);
                            await _botClient.SendMessage(_adminTelegramId, sendText, ParseMode.Html, linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true }, cancellationToken: stoppingToken);
                            await Task.Delay(1000, stoppingToken);
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Error while sending final daily statistics");
                    }

                    dailyStatistics = new DailyStatistics { CountryCode = CountryCode.Uzbekistan, DayOfStatistic = DateTime.UtcNow.ChangeTimezone(Country.Uzbekistan).DateTime };
                    _database.DailyStatistics.Add(dailyStatistics);
                    _database.SaveChanges();
                }

                if (getStdScoresResponse != null) getStdScoresCursor = getStdScoresResponse.CursorString;
                if (getTaikoScoresResponse != null) getTaikoScoresCursor = getTaikoScoresResponse.CursorString;
                if (getFruitsScoresResponse != null) getFruitsScoresCursor = getFruitsScoresResponse.CursorString;
                if (getManiaScoresResponse != null) getManiaScoresCursor = getManiaScoresResponse.CursorString;

                if (getStdScoresResponse?.Scores?.Length >= 1000 || getTaikoScoresResponse?.Scores?.Length >= 1000 || getFruitsScoresResponse?.Scores?.Length >= 1000 || getManiaScoresResponse?.Scores?.Length >= 1000)
                {
                    _logger.LogWarning("GetScores returned 1000+ scores in one mode. std={Std} taiko={Taiko} fruits={Fruits} mania={Mania}",
                        getStdScoresResponse?.Scores?.Length, getTaikoScoresResponse?.Scores?.Length, getFruitsScoresResponse?.Scores?.Length, getManiaScoresResponse?.Scores?.Length);
                }

                int stdScoresCount = Math.Max(1, getStdScoresResponse?.Scores?.Length ?? 1);
                int delay = 3000 + 1000 * (1000 / stdScoresCount);
                int clampedDelay = Math.Clamp(delay, 3000, 55_000);
                _logger.LogInformation("Processed {Count} std scores. Next GetScores in {Delay}ms.", stdScoresCount, clampedDelay);
                await _database.SaveChangesAsync(stoppingToken);

                await Task.Delay(clampedDelay, stoppingToken);
                counter++;
            }
            catch (HttpRequestException httpRequestException)
            {
                const int waitMs = 20_000;
                _logger.LogWarning("[{Service}]: status code {StatusCode}. Message: {Message}. Waiting {WaitMs}ms...",
                    nameof(ScoresObserverBackgroundService), httpRequestException.StatusCode, httpRequestException.Message, waitMs);
                await Task.Delay(waitMs, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "[ObserveScoresGetScores] Unexpected exception");
            }
    }

    private async Task ObserveScores(CancellationToken stoppingToken)
    {
        const int scoresLimit = 50;
        Dictionary<int, GetUserScoresResponse> scores = new();

        while (!stoppingToken.IsCancellationRequested)
            try
            {
                foreach (int userId in ObservedUsers)
                {
                    var userBestScores = await _osuApi.Users.GetUserScores(userId, ScoreType.Best,
                        new GetUserScoreQueryParameters { Limit = scoresLimit });
                    if (userBestScores == null)
                    {
                        _logger.LogWarning("{UserId} has no scores!", userId);
                        continue;
                    }

                    if (scores.ContainsKey(userId) && !scores[userId].Scores.Any(m => m.ModeInt != userBestScores.Scores.FirstOrDefault()?.ModeInt))
                    {
                        Score[] newScores = userBestScores.Scores.Except(scores[userId].Scores, ScoreComparer).ToArray();

                        if (newScores.Length != scoresLimit)
                        {
                            _ = Task.Run(async () =>
                            {
                                foreach (var score in newScores)
                                {
                                    try
                                    {
                                        int waitMs = 1000 + Random.Shared.Next(500, 1500);
                                        string msg = $"<b>{score.User?.Username}</b> set a <b>{score.Pp}pp</b> {GetScoreUrlWrappedInString(score.Id!.Value, "score!")}";

                                        await _botClient.SendMessage(_adminTelegramId, msg, ParseMode.Html,
                                            linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true }, cancellationToken: stoppingToken);
                                        await Task.Delay(waitMs, stoppingToken);

                                        var chatsToSend = _database.TelegramChats.Where(m => m.TrackedPlayers != null && m.TrackedPlayers.Contains(userId));
                                        foreach (var chat in chatsToSend)
                                        {
                                            await _botClient.SendMessage(chat.ChatId, msg, ParseMode.Html,
                                                linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true }, cancellationToken: stoppingToken);
                                            await Task.Delay(waitMs, stoppingToken);
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        _logger.LogError(e, "Error while sending new score notification");
                                    }
                                }
                            }, stoppingToken).ConfigureAwait(false);
                        }
                    }

                    scores[userId] = userBestScores;
                    await Task.Delay(5000, stoppingToken);
                }
            }
            catch (HttpRequestException httpRequestException)
            {
                const int waitMs = 10_000;
                _logger.LogWarning("[{Service}]: status code {StatusCode}. Message: {Message}. Waiting {WaitMs}ms...",
                    nameof(ScoresObserverBackgroundService), httpRequestException.StatusCode, httpRequestException.Message, waitMs);
                await Task.Delay(waitMs, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Unexpected exception");
            }
    }

    private async Task<UserStatistics[]> GetBestPlayersFor(string? countryCode = null)
    {
        var rankings = await _osuApi.Rankings.GetRanking(Ruleset.Osu, RankingType.Performance,
            new GetRankingQueryParameters { Country = countryCode, Filter = Filter.All });
        if (rankings == null)
        {
            _logger.LogError("Ranking is null. {CountryCode}", countryCode);
            throw new Exception("Ranking is null. See logs for details");
        }

        return rankings.Ranking!;
    }

    private async Task AddPlayersToObserverListFromSpecificCountryLeaderboard(string? countryCode = null, int count = 50)
    {
        var bestPlayersStatistics = (await GetBestPlayersFor(countryCode)).Take(count).ToArray();
        foreach (var playerStatistics in bestPlayersStatistics) ObservedUsers.Add(playerStatistics.User!.Id.Value);
    }

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

    public static List<DailyStatistics> AllDailyStatistics = [];

    private static readonly string CacheDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache", "daily_statistics");
    private static readonly string CachePath = Path.Combine(CacheDirectory, "statistics.cache");

    private async Task SaveDailyStatistics()
    {
        if (!Directory.Exists(CacheDirectory)) Directory.CreateDirectory(CacheDirectory);
        await File.WriteAllTextAsync(CachePath, JsonSerializer.Serialize(AllDailyStatistics));
    }

    private async Task LoadDailyStatistics()
    {
        if (!File.Exists(CachePath)) return;

        var list = JsonSerializer.Deserialize<List<DailyStats>>(await File.ReadAllTextAsync(CachePath));
        if (list == null) return;

        AllDailyStatistics = list.Select(m => new DailyStatistics
        {
            CountryCode = m.CountryCode,
            DayOfStatistic = m.DayOfStatistic,
            ActiveUsers = m.ActiveUsers.Select(u => new UserEntity { UserId = u.Id!.Value, UserJson = u }).ToList(),
            Scores = m.Scores.Select(s => new ScoreEntity { ScoreId = s.Id!.Value, ScoreJson = s }).ToList(),
            BeatmapsPlayed = m.BeatmapsPlayed
        }).ToList();
    }

    private static string GetScoreUrlWrappedInString(long scoreId, string text)
        => $"<a href=\"https://osu.ppy.sh/scores/{scoreId}\">{text}</a>";

    private static string BuildDailyStatisticsSendText(Playmode playmode, DailyStatistics dailyStatistics)
    {
        var passedScores = dailyStatistics.Scores.Where(m => m.ScoreJson.ModeInt == (int)playmode).ToList();
        var activeUsers = dailyStatistics.ActiveUsers
            .Where(u => passedScores.Any(s => s.ScoreJson.UserId == u.UserId))
            .ToList();

        var topActive = activeUsers
            .Select(u => new
            {
                User = u,
                Count = passedScores.Count(s => s.ScoreJson.UserId == u.UserId),
                MaxPp = passedScores.Where(s => s.ScoreJson.UserId == u.UserId).Max(s => s.ScoreJson.Pp ?? 0)
            })
            .OrderByDescending(x => x.Count)
            .ThenByDescending(x => x.MaxPp)
            .Take(5)
            .ToArray();

        var topPp = activeUsers
            .Select(u => new
            {
                User = u,
                MaxPp = passedScores.Where(s => s.ScoreJson.UserId == u.UserId).Max(s => s.ScoreJson.Pp ?? 0)
            })
            .OrderByDescending(x => x.MaxPp)
            .Take(5)
            .ToArray();

        var topMaps = passedScores
            .Where(s => s.ScoreJson.BeatmapId.HasValue)
            .GroupBy(s => s.ScoreJson.BeatmapId!.Value)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .ToArray();

        string modeName = playmode switch
        {
            Playmode.Osu => "osu!std",
            Playmode.Taiko => "osu!taiko",
            Playmode.Catch => "osu!catch",
            Playmode.Mania => "osu!mania",
            _ => playmode.ToString()
        };

        var topPpText = topPp.Length == 0
            ? ":("
            : string.Join("\n", topPp.Select((x, i) => $"{i + 1}. <b>{x.User.UserJson.Username}</b> — <i>{x.MaxPp:N0}pp</i>"));

        var topActiveText = topActive.Length == 0
            ? ":("
            : string.Join("\n", topActive.Select((x, i) => $"{i + 1}. <b>{x.User.UserJson.Username}</b> — {x.Count} scores, max <i>{x.MaxPp:N0}pp</i>"));

        var topMapsText = topMaps.Length == 0
            ? ":("
            : string.Join("\n", topMaps.Select((x, i) => $"{i + 1}. <a href=\"https://osu.ppy.sh/beatmaps/{x.Key}\">beatmap {x.Key}</a> — <b>{x.Count()} scores</b>"));

        return
            $"<b>🇺🇿 Report ({modeName}) since {dailyStatistics.DayOfStatistic:dd.MM.yyyy HH:mm}:</b>\n\n" +
            $"<b>Active players:</b> {activeUsers.Count}\n" +
            $"<b>Passed scores:</b> {passedScores.Count}\n" +
            $"<b>Unique played maps:</b> {passedScores.Select(s => s.ScoreJson.BeatmapId).Distinct().Count()}\n\n" +
            $"<b>💅 Top-5 farmers:</b>\n{topPpText}\n\n" +
            $"<b>🔥 Top-5 active players:</b>\n{topActiveText}\n\n" +
            $"<b>🎯 Top-5 played maps:</b>\n{topMapsText}";
    }
}
