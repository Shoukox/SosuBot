using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SosuBot.Database;
using SosuBot.Database.Models;
using SosuBot.ScoresObserver.Extensions;
using Country = SosuBot.ScoresObserver.Models.Country;

namespace SosuBot.ScoresObserver.Services;

/// <summary>
/// Creates one durable report delivery for each playmode of yesterday's
/// Uzbekistan statistic. It deliberately never scans older statistics.
/// </summary>
public sealed class DailyStatisticsReportOutboxBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<ScoresObserverConfiguration> configuration,
    ILogger<DailyStatisticsReportOutboxBackgroundService> logger) : BackgroundService
{
    private const long EnqueueLockId = 0x534F53554441594C;
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan ErrorDelay = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Daily statistics report outbox producer started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (configuration.Value.CreateDeliveries)
                    await QueueYesterdayReportsAsync(stoppingToken);
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to enqueue yesterday's daily statistics reports");
                await Task.Delay(ErrorDelay, stoppingToken);
            }
        }

        logger.LogInformation("Daily statistics report outbox producer stopped");
    }

    private async Task QueueYesterdayReportsAsync(CancellationToken cancellationToken)
    {
        DateTime tashkentToday = DateTime.UtcNow.ChangeTimezone(Country.Uzbekistan).Date;
        DateTime completedDay = tashkentToday.AddDays(-1);

        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        BotContext database = scope.ServiceProvider.GetRequiredService<BotContext>();
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        // The transaction-scoped lock prevents duplicate insert races when several
        // standby observer replicas run this lightweight producer simultaneously.
        await database.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({EnqueueLockId})",
            cancellationToken);

        DailyStatistics? statistic = await database.DailyStatistics
            .AsNoTracking()
            .Where(item => item.CountryCode == Models.CountryCode.Uzbekistan)
            .Where(item => item.DayOfStatistic >= completedDay && item.DayOfStatistic < tashkentToday)
            .OrderByDescending(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (statistic is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        long? adminChatId = await database.OsuUsers
            .AsNoTracking()
            .Where(user => user.IsAdmin)
            .OrderBy(user => user.TelegramId)
            .Select(user => (long?)user.TelegramId)
            .FirstOrDefaultAsync(cancellationToken);
        if (adminChatId is null)
        {
            await transaction.CommitAsync(cancellationToken);
            logger.LogWarning(
                "Daily statistic {DailyStatisticsId} cannot be queued because no administrator is configured",
                statistic.Id);
            return;
        }

        HashSet<Playmode> existingModes = await database.DailyStatisticsReportDeliveries
            .AsNoTracking()
            .Where(delivery => delivery.DailyStatisticsId == statistic.Id)
            .Select(delivery => delivery.Mode)
            .ToHashSetAsync(cancellationToken);

        DateTimeOffset now = DateTimeOffset.UtcNow;
        var added = 0;
        foreach (Playmode mode in Enum.GetValues<Playmode>())
        {
            if (existingModes.Contains(mode))
                continue;

            database.DailyStatisticsReportDeliveries.Add(new DailyStatisticsReportDelivery
            {
                DailyStatisticsId = statistic.Id,
                Mode = mode,
                ChatId = adminChatId.Value,
                CreatedAtUtc = now,
                AvailableAtUtc = now.AddSeconds((int)mode)
            });
            added++;
        }

        if (added > 0)
            await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        if (added > 0)
        {
            logger.LogInformation(
                "Queued {DeliveryCount} daily statistic reports for {StatisticDay} (statistic {DailyStatisticsId})",
                added,
                completedDay,
                statistic.Id);
        }
    }
}
