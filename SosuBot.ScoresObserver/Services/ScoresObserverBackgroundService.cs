using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using OsuApi.BanchoV2;
using OsuApi.BanchoV2.Clients.Rankings.HttpIO;
using OsuApi.BanchoV2.Clients.Scores.HttpIO;
using OsuApi.BanchoV2.Clients.Users.HttpIO;
using OsuApi.BanchoV2.Models;
using OsuApi.BanchoV2.Users.Models;
using SosuBot.Database;
using SosuBot.Database.Models;
using SosuBot.ScoresObserver.Extensions;
using SosuBot.ScoresObserver.Monitoring;
using Country = SosuBot.ScoresObserver.Models.Country;

namespace SosuBot.ScoresObserver.Services;

public sealed class ScoresObserverBackgroundService(
    IServiceProvider serviceProvider,
    IServiceScopeFactory scopeFactory,
    NpgsqlDataSource dataSource,
    IOptions<ScoresObserverConfiguration> configuration,
    ObserverMetrics metrics,
    ILogger<ScoresObserverBackgroundService> logger) : BackgroundService
{
    private const long LeaderLockId = 0x534F5355424F5401;
    private const string StandardFeed = "osu";
    private const string TaikoFeed = "taiko";
    private const string FruitsFeed = "fruits";
    private const string ManiaFeed = "mania";
    private readonly ScoresObserverConfiguration _configuration = configuration.Value;
    private BanchoApiV2 _osuApi = null!;
    private UserStatisticsCacheDatabase _userStatisticsCache = null!;
    private HashSet<int> _leaderboardPlayers = [];
    private DateTimeOffset _nextLeaderboardRefreshUtc = DateTimeOffset.MinValue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Scores observer started");

        while (!stoppingToken.IsCancellationRequested)
        {
            metrics.SetLeader(false);
            try
            {
                await using NpgsqlConnection leaderConnection = await dataSource.OpenConnectionAsync(stoppingToken);
                if (!await TryAcquireLeaderLock(leaderConnection, stoppingToken))
                {
                    logger.LogInformation("Another ScoresObserver instance is active; waiting for leadership");
                    await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
                    continue;
                }

                logger.LogInformation("Acquired ScoresObserver PostgreSQL advisory lock");
                metrics.SetLeader(true);
                _osuApi = serviceProvider.GetRequiredService<BanchoApiV2>();
                _userStatisticsCache = serviceProvider.GetRequiredService<UserStatisticsCacheDatabase>();
                using CancellationTokenSource leaderTokenSource =
                    CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                Task observerTask = Task.WhenAll(
                    ObserveTrackedPlayerScores(leaderTokenSource.Token),
                    ObserveCountryScores(leaderTokenSource.Token));
                Task connectionMonitorTask = MonitorLeaderConnection(
                    leaderConnection,
                    leaderTokenSource.Token);

                Task completedTask = await Task.WhenAny(observerTask, connectionMonitorTask);
                if (completedTask == connectionMonitorTask)
                {
                    leaderTokenSource.Cancel();
                    await IgnoreExpectedCancellation(observerTask, stoppingToken);
                    await connectionMonitorTask;
                    throw new InvalidOperationException("ScoresObserver leader connection monitor stopped unexpectedly.");
                }

                leaderTokenSource.Cancel();
                await IgnoreExpectedCancellation(connectionMonitorTask, stoppingToken);
                await observerTask;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                metrics.SetLeader(false);
                break;
            }
            catch (Exception exception)
            {
                metrics.SetLeader(false);
                logger.LogError(exception, "ScoresObserver leadership was lost; retrying");
                await Task.Delay(_configuration.ErrorDelay, stoppingToken);
            }
        }

        metrics.SetLeader(false);
        logger.LogInformation("Scores observer is stopping");
    }

    private static async Task<bool> TryAcquireLeaderLock(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = "SELECT pg_try_advisory_lock($1)";
        command.Parameters.AddWithValue(LeaderLockId);
        object? result = await command.ExecuteScalarAsync(cancellationToken);
        return result is true;
    }

    private static async Task MonitorLeaderConnection(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        using PeriodicTimer timer = new(TimeSpan.FromSeconds(10));
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            await using NpgsqlCommand command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            await command.ExecuteScalarAsync(cancellationToken);
        }
    }

    private static async Task IgnoreExpectedCancellation(Task task, CancellationToken stoppingToken)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Expected host shutdown.
        }
        catch (OperationCanceledException)
        {
            // Expected cancellation after the observer loops have completed.
        }
    }

    private async Task ObserveTrackedPlayerScores(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                IReadOnlyList<int> observedPlayers = await GetObservedPlayers(stoppingToken);
                await MarkUnobservedCheckpointsInactive(observedPlayers, stoppingToken);
                metrics.SetObservedPlayers(observedPlayers.Count);

                if (observedPlayers.Count == 0)
                {
                    logger.LogWarning("No players are currently available for score observation");
                    await Task.Delay(_configuration.EmptyObserverDelay, stoppingToken);
                    continue;
                }

                var cycleSucceeded = true;
                foreach (int playerId in observedPlayers)
                {
                    try
                    {
                        await ObservePlayer(playerId, stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception exception)
                    {
                        cycleSucceeded = false;
                        logger.LogError(exception, "Failed to observe best scores for osu! user {PlayerId}", playerId);
                    }

                    await Task.Delay(_configuration.UserPollDelay, stoppingToken);
                }

                metrics.RecordPoll("tracked_best", cycleSucceeded);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                metrics.RecordPoll("tracked_best", false);
                logger.LogError(exception, "Unexpected error in tracked-score polling cycle");
                await Task.Delay(_configuration.ErrorDelay, stoppingToken);
            }
        }
    }

    private async Task<IReadOnlyList<int>> GetObservedPlayers(CancellationToken cancellationToken)
    {
        List<int> trackedPlayers;
        await using (AsyncServiceScope scope = scopeFactory.CreateAsyncScope())
        {
            BotContext database = scope.ServiceProvider.GetRequiredService<BotContext>();
            await SynchronizeTrackedPlayerSubscriptions(database, cancellationToken);
            trackedPlayers = await database.TrackedPlayerSubscriptions
                .AsNoTracking()
                .Select(subscription => subscription.PlayerId)
                .Distinct()
                .ToListAsync(cancellationToken);
        }

        if (DateTimeOffset.UtcNow >= _nextLeaderboardRefreshUtc)
            await RefreshLeaderboardPlayers(cancellationToken);

        return trackedPlayers
            .Concat(_leaderboardPlayers)
            .Distinct()
            .ToArray();
    }

    private static async Task SynchronizeTrackedPlayerSubscriptions(
        BotContext database,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        await database.Database.ExecuteSqlInterpolatedAsync(
            $$"""
              INSERT INTO "TrackedPlayerSubscriptions" ("ChatId", "PlayerId", "StartedAtUtc")
              SELECT chat."ChatId", player."PlayerId", {{now}}
              FROM "TelegramChats" AS chat
              CROSS JOIN LATERAL unnest(chat."TrackedPlayers") AS player("PlayerId")
              WHERE chat."TrackedPlayers" IS NOT NULL
              ON CONFLICT ("ChatId", "PlayerId") DO NOTHING
              """,
            cancellationToken);
        await database.Database.ExecuteSqlRawAsync(
            """
            DELETE FROM "TrackedPlayerSubscriptions" AS subscription
            WHERE NOT EXISTS (
                SELECT 1
                FROM "TelegramChats" AS chat
                WHERE chat."ChatId" = subscription."ChatId"
                  AND chat."TrackedPlayers" IS NOT NULL
                  AND subscription."PlayerId" = ANY(chat."TrackedPlayers")
            )
            """,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task MarkUnobservedCheckpointsInactive(
        IReadOnlyCollection<int> observedPlayers,
        CancellationToken cancellationToken)
    {
        int[] observedPlayerIds = observedPlayers.ToArray();
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        BotContext database = scope.ServiceProvider.GetRequiredService<BotContext>();
        int changed = await database.TrackedPlayerCheckpoints
            .Where(checkpoint => checkpoint.IsActive)
            .Where(checkpoint => !observedPlayerIds.Contains(checkpoint.PlayerId))
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(checkpoint => checkpoint.IsActive, false),
                cancellationToken);

        if (changed > 0)
            logger.LogInformation("Marked {CheckpointCount} unobserved player checkpoints inactive", changed);
    }

    private async Task RefreshLeaderboardPlayers(CancellationToken cancellationToken)
    {
        try
        {
            Task<UserStatistics[]> countryPlayersTask = GetBestPlayers("uz", cancellationToken);
            Task<UserStatistics[]> globalPlayersTask = GetBestPlayers(null, cancellationToken);
            await Task.WhenAll(countryPlayersTask, globalPlayersTask);

            _leaderboardPlayers = countryPlayersTask.Result
                .Concat(globalPlayersTask.Result)
                .Take(_configuration.LeaderboardPlayers * 2)
                .Select(statistics => statistics.User?.Id)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .ToHashSet();
            _nextLeaderboardRefreshUtc = DateTimeOffset.UtcNow + _configuration.LeaderboardRefreshInterval;
            logger.LogInformation("Refreshed leaderboard observer list with {PlayerCount} players",
                _leaderboardPlayers.Count);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _nextLeaderboardRefreshUtc = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(5);
            logger.LogWarning(exception,
                "Could not refresh leaderboard players; retaining {PlayerCount} previous entries",
                _leaderboardPlayers.Count);
        }
    }

    private async Task<UserStatistics[]> GetBestPlayers(string? countryCode, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Rankings? rankings = await _osuApi.Rankings.GetRanking(
            Ruleset.Osu,
            RankingType.Performance,
            new GetRankingQueryParameters { Country = countryCode, Filter = Filter.All });
        cancellationToken.ThrowIfCancellationRequested();

        if (rankings?.Ranking is null)
            throw new InvalidOperationException($"osu! ranking response was empty for country '{countryCode ?? "global"}'");

        return rankings.Ranking.Take(_configuration.LeaderboardPlayers).ToArray();
    }

    private async Task ObservePlayer(int playerId, CancellationToken cancellationToken)
    {
        GetUserScoresResponse? response = await _osuApi.Users.GetUserScores(
            playerId,
            ScoreType.Best,
            new GetUserScoreQueryParameters { Limit = _configuration.ScoresLimit });
        cancellationToken.ThrowIfCancellationRequested();

        if (response?.Scores is not { Length: > 0 } bestScores)
        {
            logger.LogDebug("osu! user {PlayerId} has no best scores", playerId);
            return;
        }

        (int scoreCount, int deliveryCount) = await PersistTrackedScores(playerId, bestScores, cancellationToken);
        metrics.RecordTrackedScores(scoreCount, deliveryCount);
        if (scoreCount > 0)
        {
            logger.LogInformation(
                "Persisted {ScoreCount} new tracked scores for player {PlayerId} and queued {DeliveryCount} deliveries",
                scoreCount,
                playerId,
                deliveryCount);
        }
    }

    private async Task<(int ScoreCount, int DeliveryCount)> PersistTrackedScores(
        int playerId,
        IReadOnlyCollection<Score> bestScores,
        CancellationToken cancellationToken)
    {
        long[] currentScoreIds = bestScores
            .Where(score => score.Id.HasValue)
            .Select(score => score.Id!.Value)
            .Distinct()
            .ToArray();
        int? currentMode = bestScores.FirstOrDefault()?.ModeInt;
        DateTimeOffset now = DateTimeOffset.UtcNow;

        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        BotContext database = scope.ServiceProvider.GetRequiredService<BotContext>();
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        List<TrackedPlayerSubscription> subscriptions = await database.TrackedPlayerSubscriptions
            .AsNoTracking()
            .Where(subscription => subscription.PlayerId == playerId)
            .ToListAsync(cancellationToken);

        TrackedPlayerCheckpoint? checkpoint = await database.TrackedPlayerCheckpoints
            .SingleOrDefaultAsync(item => item.PlayerId == playerId, cancellationToken);
        bool needsBaseline = checkpoint is null || !checkpoint.IsActive;
        if (checkpoint is null)
        {
            checkpoint = new TrackedPlayerCheckpoint
            {
                PlayerId = playerId,
                Mode = currentMode,
                BestScoreIds = currentScoreIds.ToList(),
                UpdatedAtUtc = now,
                IsActive = true
            };
            database.TrackedPlayerCheckpoints.Add(checkpoint);
        }

        HashSet<long> previousScoreIds = checkpoint.BestScoreIds.ToHashSet();
        bool modeChanged = checkpoint.Mode != currentMode;
        DateTimeOffset earliestNewScore = checkpoint.UpdatedAtUtc - TimeSpan.FromMinutes(5);

        Score[] newScores;
        if (modeChanged)
        {
            newScores = [];
        }
        else if (needsBaseline)
        {
            DateTimeOffset? earliestSubscription = subscriptions.Count == 0
                ? null
                : subscriptions.Min(subscription => subscription.StartedAtUtc);
            newScores = earliestSubscription is null
                ? []
                : bestScores
                    .Where(score => score.Id.HasValue && score.EndedAt.HasValue)
                    .Where(score => score.EndedAt!.Value >= earliestSubscription.Value)
                    .OrderBy(score => score.EndedAt)
                    .ToArray();
        }
        else
        {
            newScores = bestScores
                .Where(score => score.Id.HasValue && !previousScoreIds.Contains(score.Id.Value))
                .Where(score => score.EndedAt is null || score.EndedAt.Value >= earliestNewScore)
                .OrderBy(score => score.EndedAt)
                .ToArray();
        }

        checkpoint.Mode = currentMode;
        checkpoint.BestScoreIds = currentScoreIds.ToList();
        checkpoint.UpdatedAtUtc = now;
        checkpoint.IsActive = true;

        if (needsBaseline)
        {
            logger.LogInformation(
                "Initialized score checkpoint for player {PlayerId}; {CandidateCount} post-subscription scores will be persisted",
                playerId,
                newScores.Length);
        }

        if (modeChanged)
        {
            logger.LogInformation(
                "Player {PlayerId} changed the default ruleset; refreshed checkpoint without creating notifications",
                playerId);
        }

        if (newScores.Length == 0)
        {
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return (0, 0);
        }

        if (!_configuration.CreateDeliveries)
        {
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            logger.LogInformation(
                "Advanced player {PlayerId} checkpoint across {ScoreCount} scores while delivery creation is disabled",
                playerId,
                newScores.Length);
            return (0, 0);
        }

        long? adminChatId = await database.OsuUsers
            .AsNoTracking()
            .Where(user => user.IsAdmin)
            .Select(user => (long?)user.TelegramId)
            .FirstOrDefaultAsync(cancellationToken);

        long[] newScoreIds = newScores.Select(score => score.Id!.Value).ToArray();
        Dictionary<long, TrackedScoreEvent> existingEvents = await database.TrackedScoreEvents
            .Include(scoreEvent => scoreEvent.Deliveries)
            .Where(scoreEvent => newScoreIds.Contains(scoreEvent.ScoreId))
            .ToDictionaryAsync(scoreEvent => scoreEvent.ScoreId, cancellationToken);

        var deliveryCount = 0;
        foreach (Score score in newScores)
        {
            long scoreId = score.Id!.Value;
            DateTimeOffset occurredAt = score.EndedAt?.ToUniversalTime() ?? now;
            if (!existingEvents.TryGetValue(scoreId, out TrackedScoreEvent? scoreEvent))
            {
                scoreEvent = new TrackedScoreEvent
                {
                    ScoreId = scoreId,
                    PlayerId = playerId,
                    ScoreJson = score,
                    OccurredAtUtc = occurredAt,
                    DetectedAtUtc = now
                };
                database.TrackedScoreEvents.Add(scoreEvent);
                existingEvents.Add(scoreId, scoreEvent);
            }

            Dictionary<long, bool> recipients = subscriptions
                .Where(subscription => subscription.StartedAtUtc <= occurredAt)
                .ToDictionary(subscription => subscription.ChatId, _ => false);
            if (adminChatId.HasValue)
                recipients[adminChatId.Value] = true;

            foreach ((long chatId, bool isAdminRecipient) in recipients)
            {
                if (scoreEvent.Deliveries.Any(delivery => delivery.ChatId == chatId))
                    continue;

                scoreEvent.Deliveries.Add(new TrackedScoreDelivery
                {
                    ScoreId = scoreId,
                    ChatId = chatId,
                    IsAdminRecipient = isAdminRecipient,
                    CreatedAtUtc = now,
                    AvailableAtUtc = now
                });
                deliveryCount++;
            }
        }

        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return (newScores.Length, deliveryCount);
    }

    private async Task ObserveCountryScores(CancellationToken stoppingToken)
    {
        Dictionary<string, string?> cursors = await LoadScoreFeedCursors(stoppingToken);
        ulong iteration = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _userStatisticsCache.CacheIfNeeded(stoppingToken);

                ScoresResponse? stdResponse = await _osuApi.Scores.GetScores(new ScoresQueryParameters
                {
                    CursorString = cursors.GetValueOrDefault(StandardFeed),
                    Ruleset = Ruleset.Osu
                });
                ScoresResponse? taikoResponse = iteration % 8 == 0
                    ? await _osuApi.Scores.GetScores(new ScoresQueryParameters
                    {
                        CursorString = cursors.GetValueOrDefault(TaikoFeed),
                        Ruleset = Ruleset.Taiko
                    })
                    : null;
                ScoresResponse? fruitsResponse = iteration % 12 == 0
                    ? await _osuApi.Scores.GetScores(new ScoresQueryParameters
                    {
                        CursorString = cursors.GetValueOrDefault(FruitsFeed),
                        Ruleset = Ruleset.Fruits
                    })
                    : null;
                ScoresResponse? maniaResponse = iteration % 4 == 0
                    ? await _osuApi.Scores.GetScores(new ScoresQueryParameters
                    {
                        CursorString = cursors.GetValueOrDefault(ManiaFeed),
                        Ruleset = Ruleset.Mania
                    })
                    : null;
                stoppingToken.ThrowIfCancellationRequested();

                List<Score> allScores = [];
                AddScores(allScores, stdResponse, Ruleset.Osu, Playmode.Osu);
                AddScores(allScores, taikoResponse, Ruleset.Taiko, Playmode.Taiko);
                AddScores(allScores, fruitsResponse, Ruleset.Fruits, Playmode.Catch);
                AddScores(allScores, maniaResponse, Ruleset.Mania, Playmode.Mania);

                DateTime tashkentToday = DateTime.UtcNow.ChangeTimezone(Country.Uzbekistan).Date;
                Score[] countryScores = allScores
                    .Where(score => score.UserId.HasValue && score.EndedAt.HasValue)
                    .Where(score => score.EndedAt!.Value.ChangeTimezone(Country.Uzbekistan).Date >= tashkentToday)
                    .Where(score => _userStatisticsCache.ContainsUserStatistics(score.UserId!.Value))
                    .ToArray();

                var scoresWithUsers = new List<(Score Score, UserStatistics UserStatistics)>();
                foreach (Score score in countryScores)
                {
                    UserStatistics? userStatistics = await _userStatisticsCache.GetUserStatistics(
                        score.UserId!.Value,
                        stoppingToken);
                    if (userStatistics?.User is not null)
                        scoresWithUsers.Add((score, userStatistics));
                }

                Dictionary<string, string?> cursorUpdates = [];
                AddCursorUpdate(cursorUpdates, StandardFeed, stdResponse);
                AddCursorUpdate(cursorUpdates, TaikoFeed, taikoResponse);
                AddCursorUpdate(cursorUpdates, FruitsFeed, fruitsResponse);
                AddCursorUpdate(cursorUpdates, ManiaFeed, maniaResponse);

                await PersistDailyScores(tashkentToday, scoresWithUsers, cursorUpdates, stoppingToken);
                foreach ((string source, string? cursor) in cursorUpdates)
                    cursors[source] = cursor;
                metrics.RecordPoll("global_scores", true);

                int stdScoreCount = Math.Max(1, stdResponse?.Scores?.Length ?? 1);
                int delayMilliseconds = Math.Clamp(3000 + 1000 * (1000 / stdScoreCount), 3000, 55_000);
                await Task.Delay(delayMilliseconds, stoppingToken);
                iteration++;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (HttpRequestException exception)
            {
                metrics.RecordPoll("global_scores", false);
                logger.LogWarning(exception,
                    "Global score feed returned HTTP status {StatusCode}; retrying after {Delay}",
                    exception.StatusCode,
                    _configuration.ErrorDelay);
                await Task.Delay(_configuration.ErrorDelay, stoppingToken);
            }
            catch (Exception exception)
            {
                metrics.RecordPoll("global_scores", false);
                logger.LogError(exception, "Unexpected error while processing the global score feed");
                await Task.Delay(_configuration.ErrorDelay, stoppingToken);
            }
        }
    }

    private async Task<Dictionary<string, string?>> LoadScoreFeedCursors(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        BotContext database = scope.ServiceProvider.GetRequiredService<BotContext>();
        return await database.ScoreFeedCheckpoints
            .AsNoTracking()
            .ToDictionaryAsync(checkpoint => checkpoint.Source, checkpoint => checkpoint.Cursor, cancellationToken);
    }

    private static void AddCursorUpdate(
        IDictionary<string, string?> updates,
        string source,
        ScoresResponse? response)
    {
        if (!string.IsNullOrWhiteSpace(response?.CursorString))
            updates[source] = response.CursorString;
    }

    private static void AddScores(
        ICollection<Score> target,
        ScoresResponse? response,
        string ruleset,
        Playmode playmode)
    {
        if (response?.Scores is null) return;
        foreach (Score score in response.Scores)
            target.Add(score with { Mode = ruleset, ModeInt = (int)playmode });
    }

    private async Task PersistDailyScores(
        DateTime day,
        IReadOnlyCollection<(Score Score, UserStatistics UserStatistics)> scores,
        IReadOnlyDictionary<string, string?> cursorUpdates,
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        BotContext database = scope.ServiceProvider.GetRequiredService<BotContext>();
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        DailyStatistics? dailyStatistics = await database.DailyStatistics
            .Include(statistics => statistics.ActiveUsers)
            .Where(statistics => statistics.CountryCode == Models.CountryCode.Uzbekistan)
            .Where(statistics => statistics.DayOfStatistic == day)
            .OrderByDescending(statistics => statistics.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (dailyStatistics is null)
        {
            dailyStatistics = new DailyStatistics
            {
                CountryCode = Models.CountryCode.Uzbekistan,
                DayOfStatistic = day
            };
            database.DailyStatistics.Add(dailyStatistics);
            await database.SaveChangesAsync(cancellationToken);
        }

        long[] scoreIds = scores
            .Where(item => item.Score.Id.HasValue)
            .Select(item => item.Score.Id!.Value)
            .Distinct()
            .ToArray();
        Dictionary<long, ScoreEntity> existingScores = await database.ScoreEntity
            .Where(score => scoreIds.Contains(score.ScoreId))
            .ToDictionaryAsync(score => score.ScoreId, cancellationToken);

        int[] userIds = scores
            .Select(item => item.UserStatistics.User?.Id)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToArray();
        Dictionary<int, UserEntity> existingUsers = await database.UserEntity
            .Where(user => userIds.Contains(user.UserId))
            .ToDictionaryAsync(user => user.UserId, cancellationToken);
        HashSet<int> activeUserIds = dailyStatistics.ActiveUsers.Select(user => user.UserId).ToHashSet();

        foreach ((Score score, UserStatistics userStatistics) in scores)
        {
            if (!score.Id.HasValue || userStatistics.User?.Id is not int userId)
                continue;

            if (existingScores.TryGetValue(score.Id.Value, out ScoreEntity? existingScore))
            {
                existingScore.ScoreJson = score;
                existingScore.DailyStatisticsId ??= dailyStatistics.Id;
            }
            else
            {
                var scoreEntity = new ScoreEntity
                {
                    ScoreId = score.Id.Value,
                    ScoreJson = score,
                    DailyStatisticsId = dailyStatistics.Id
                };
                database.ScoreEntity.Add(scoreEntity);
                existingScores.Add(scoreEntity.ScoreId, scoreEntity);
            }

            if (!existingUsers.TryGetValue(userId, out UserEntity? userEntity))
            {
                userEntity = new UserEntity { UserId = userId, UserJson = userStatistics.User };
                database.UserEntity.Add(userEntity);
                existingUsers.Add(userId, userEntity);
            }
            else
            {
                userEntity.UserJson = userStatistics.User;
            }

            if (activeUserIds.Add(userId))
                dailyStatistics.ActiveUsers.Add(userEntity);

            if (score.BeatmapId.HasValue && !dailyStatistics.BeatmapsPlayed.Contains(score.BeatmapId.Value))
                dailyStatistics.BeatmapsPlayed.Add(score.BeatmapId.Value);
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        foreach ((string source, string? cursor) in cursorUpdates)
        {
            ScoreFeedCheckpoint? checkpoint = await database.ScoreFeedCheckpoints
                .SingleOrDefaultAsync(item => item.Source == source, cancellationToken);
            if (checkpoint is null)
            {
                database.ScoreFeedCheckpoints.Add(new ScoreFeedCheckpoint
                {
                    Source = source,
                    Cursor = cursor,
                    UpdatedAtUtc = now
                });
            }
            else
            {
                checkpoint.Cursor = cursor;
                checkpoint.UpdatedAtUtc = now;
            }
        }

        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
