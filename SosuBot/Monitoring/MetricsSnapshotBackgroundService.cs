using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SosuBot.Database;
using SosuBot.Database.Models;
using SosuBot.Services;
using System.Diagnostics;

namespace SosuBot.Monitoring;

public sealed class MetricsSnapshotBackgroundService(
    BotMetrics metrics,
    BeatmapFileCache beatmapFileCache,
    IServiceProvider serviceProvider,
    ILogger<MetricsSnapshotBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(TimeSpan.FromSeconds(15));
        int iteration = 0;

        do
        {
            metrics.RefreshActiveUsers(DateTimeOffset.UtcNow);

            if (iteration++ % 4 == 0)
            {
                await RefreshDatabaseMetrics(stoppingToken);
                await RefreshBeatmapCacheMetrics(stoppingToken);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RefreshDatabaseMetrics(CancellationToken cancellationToken)
    {
        long startedAt = Stopwatch.GetTimestamp();
        try
        {
            await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();
            BotContext database = scope.ServiceProvider.GetRequiredService<BotContext>();

            int registeredUsers = await database.OsuUsers.CountAsync(cancellationToken);
            int dailyStatistics = await database.DailyStatistics.CountAsync(cancellationToken);
            int scores = await database.ScoreEntity.CountAsync(cancellationToken);
            int cachedUsers = await database.UserEntity.CountAsync(cancellationToken);

            Dictionary<string, int> registeredUsersByMode = await database.OsuUsers
                .AsNoTracking()
                .GroupBy(user => user.OsuMode)
                .Select(group => new { Mode = group.Key, Count = group.Count() })
                .ToDictionaryAsync(
                    item => item.Mode.ToString().ToLowerInvariant(),
                    item => item.Count,
                    cancellationToken);

            var chats = await database.TelegramChats
                .AsNoTracking()
                .Select(chat => new
                {
                    chat.LanguageCode,
                    chat.ChatMembers,
                    chat.TrackedPlayers
                })
                .ToListAsync(cancellationToken);
            int knownChats = chats.Count;
            int knownChatMembers = chats.Sum(chat => chat.ChatMembers?.Count ?? 0);
            int trackedChats = chats.Count(chat => chat.TrackedPlayers is { Count: > 0 });
            int trackedPlayers = chats.Sum(chat => chat.TrackedPlayers?.Count ?? 0);
            IReadOnlyDictionary<string, int> chatsByLanguage = chats
                .GroupBy(chat => NormalizeLanguage(chat.LanguageCode))
                .ToDictionary(group => group.Key, group => group.Count());

            DateTimeOffset now = DateTimeOffset.UtcNow;
            IQueryable<TrackedScoreDelivery> pendingScoreDeliveries = database.TrackedScoreDeliveries
                .AsNoTracking()
                .Where(delivery =>
                    delivery.SentAtUtc == null &&
                    delivery.CancelledAtUtc == null &&
                    delivery.FailedAtUtc == null);
            int pendingScoreDeliveryCount = await pendingScoreDeliveries.CountAsync(cancellationToken);
            int failedScoreDeliveryCount = await database.TrackedScoreDeliveries
                .AsNoTracking()
                .CountAsync(delivery => delivery.FailedAtUtc != null, cancellationToken);
            DateTimeOffset? oldestPendingScoreDelivery = await pendingScoreDeliveries
                .MinAsync(delivery => (DateTimeOffset?)delivery.CreatedAtUtc, cancellationToken);
            double oldestPendingScoreDeliveryAgeSeconds = oldestPendingScoreDelivery is null
                ? 0
                : Math.Max(0, (now - oldestPendingScoreDelivery.Value).TotalSeconds);

            int pendingDailyReportDeliveryCount = await database.DailyStatisticsReportDeliveries
                .AsNoTracking()
                .CountAsync(delivery =>
                    delivery.SentAtUtc == null &&
                    delivery.CancelledAtUtc == null &&
                    delivery.FailedAtUtc == null,
                    cancellationToken);
            int failedDailyReportDeliveryCount = await database.DailyStatisticsReportDeliveries
                .AsNoTracking()
                .CountAsync(delivery => delivery.FailedAtUtc != null, cancellationToken);

            List<int> latestDailyStatisticIds = await database.DailyStatistics
                .AsNoTracking()
                .GroupBy(statistic => statistic.CountryCode)
                .Select(group => group
                    .OrderByDescending(statistic => statistic.DayOfStatistic)
                    .ThenByDescending(statistic => statistic.Id)
                    .Select(statistic => statistic.Id)
                    .First())
                .ToListAsync(cancellationToken);

            List<DailyStatisticMetric> latestDailyStatisticMetrics = await database.DailyStatistics
                .AsNoTracking()
                .Where(statistic => latestDailyStatisticIds.Contains(statistic.Id))
                .Select(statistic => new DailyStatisticMetric(
                    statistic.CountryCode,
                    statistic.DayOfStatistic,
                    statistic.ActiveUsers.Count,
                    statistic.Scores.Count,
                    statistic.BeatmapsPlayed.Count))
                .ToListAsync(cancellationToken);

            IReadOnlyList<CommandUsageMetric> commandUsage = await LoadCommandUsage(database, cancellationToken);

            metrics.SetDatabaseMetrics(new DatabaseMetricsSnapshot(
                registeredUsers,
                knownChats,
                knownChatMembers,
                dailyStatistics,
                scores,
                cachedUsers,
                trackedChats,
                trackedPlayers,
                pendingScoreDeliveryCount,
                failedScoreDeliveryCount,
                oldestPendingScoreDeliveryAgeSeconds,
                pendingDailyReportDeliveryCount,
                failedDailyReportDeliveryCount,
                registeredUsersByMode,
                chatsByLanguage,
                latestDailyStatisticMetrics,
                commandUsage));
            metrics.RecordDatabaseMetricsRefresh(true, Stopwatch.GetElapsedTime(startedAt).TotalSeconds);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            metrics.RecordDatabaseMetricsRefresh(false, Stopwatch.GetElapsedTime(startedAt).TotalSeconds);
            logger.LogError(exception, "Failed to refresh database-backed metrics");
        }
    }

    private async Task RefreshBeatmapCacheMetrics(CancellationToken cancellationToken)
    {
        try
        {
            long cachedBeatmapFiles = await beatmapFileCache.CountCachedBeatmapFilesAsync(cancellationToken);
            metrics.SetCachedBeatmapFiles(cachedBeatmapFiles);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Failed to refresh beatmap cache metrics");
        }
    }

    private static string NormalizeLanguage(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
            return "other";

        string normalized = languageCode.Trim().ToLowerInvariant();
        if (normalized.StartsWith("ru", StringComparison.Ordinal)) return "ru";
        if (normalized.StartsWith("en", StringComparison.Ordinal)) return "en";
        if (normalized.StartsWith("de", StringComparison.Ordinal)) return "de";
        return "other";
    }

    private static async Task<IReadOnlyList<CommandUsageMetric>> LoadCommandUsage(
        BotContext database,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        (string Name, DateTimeOffset? Since)[] windows =
        [
            ("5m", now.AddMinutes(-5)),
            ("1h", now.AddHours(-1)),
            ("24h", now.AddHours(-24)),
            ("7d", now.AddDays(-7)),
            ("all", null)
        ];

        var result = new List<CommandUsageMetric>();
        foreach ((string windowName, DateTimeOffset? since) in windows)
        {
            IQueryable<CommandUsageAggregate> query = database.CommandUsageAggregates.AsNoTracking();
            if (since.HasValue)
                query = query.Where(usage => usage.BucketStartUtc >= since.Value);

            List<CommandUsageAggregateResult> aggregates = await query
                .GroupBy(usage => usage.Command)
                .Select(group => new CommandUsageAggregateResult(
                    group.Key,
                    group.Sum(usage => usage.SuccessCount),
                    group.Sum(usage => usage.ErrorCount),
                    group.Sum(usage => usage.CancelledCount),
                    group.Sum(usage => usage.TotalDurationMilliseconds)))
                .ToListAsync(cancellationToken);

            result.AddRange(aggregates.Select(aggregate =>
            {
                long totalCount = aggregate.SuccessCount + aggregate.ErrorCount + aggregate.CancelledCount;
                double averageDurationSeconds = totalCount == 0
                    ? 0
                    : aggregate.TotalDurationMilliseconds / 1000d / totalCount;
                return new CommandUsageMetric(
                    aggregate.Command,
                    windowName,
                    aggregate.SuccessCount,
                    aggregate.ErrorCount,
                    aggregate.CancelledCount,
                    averageDurationSeconds);
            }));
        }

        return result;
    }

    private sealed record CommandUsageAggregateResult(
        string Command,
        long SuccessCount,
        long ErrorCount,
        long CancelledCount,
        long TotalDurationMilliseconds);
}
