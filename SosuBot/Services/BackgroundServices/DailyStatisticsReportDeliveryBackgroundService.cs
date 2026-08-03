using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OsuApi.BanchoV2;
using SosuBot.Database;
using SosuBot.Database.Models;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace SosuBot.Services.BackgroundServices;

/// <summary>
/// Builds and sends daily report messages from the durable outbox populated by
/// SosuBot.ScoresObserver. PostgreSQL leases allow multiple bot replicas to run
/// the dispatcher without normally processing the same row concurrently.
/// </summary>
public sealed class DailyStatisticsReportDeliveryBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<DailyStatisticsReportDeliveryBackgroundService> logger) : BackgroundService
{
    private const int MaxAttempts = 5;
    private static readonly TimeSpan EmptyQueueDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ClaimFailureDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan LeaseRenewalInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan PerChatSendInterval = TimeSpan.FromMilliseconds(1100);

    private readonly string _workerId =
        $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Daily statistics report delivery worker {WorkerId} started", _workerId);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                DeliveryKey? delivery = await ClaimPendingDeliveryAsync(stoppingToken);
                if (delivery is null)
                {
                    await Task.Delay(EmptyQueueDelay, stoppingToken);
                    continue;
                }

                await ProcessDeliveryAsync(delivery.Value, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to claim a daily statistics report delivery");
                await Task.Delay(ClaimFailureDelay, stoppingToken);
            }
        }

        logger.LogInformation("Daily statistics report delivery worker {WorkerId} stopped", _workerId);
    }

    private async Task<DeliveryKey?> ClaimPendingDeliveryAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        BotContext database = scope.ServiceProvider.GetRequiredService<BotContext>();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        // A process may stop during its final attempt. Once that lease expires,
        // close the row instead of sending it more than MaxAttempts times.
        await database.DailyStatisticsReportDeliveries
            .Where(delivery =>
                delivery.SentAtUtc == null &&
                delivery.CancelledAtUtc == null &&
                delivery.FailedAtUtc == null &&
                delivery.Attempts >= MaxAttempts &&
                (delivery.LockedUntilUtc == null || delivery.LockedUntilUtc <= now))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(delivery => delivery.FailedAtUtc, _ => now)
                .SetProperty(delivery => delivery.LockedUntilUtc, _ => null)
                .SetProperty(delivery => delivery.LockedBy, _ => null), cancellationToken);

        DailyStatisticsReportDelivery? claimed = await database.DailyStatisticsReportDeliveries
            .FromSqlInterpolated($$"""
                SELECT delivery.*
                FROM "DailyStatisticsReportDeliveries" AS delivery
                WHERE delivery."SentAtUtc" IS NULL
                  AND delivery."CancelledAtUtc" IS NULL
                  AND delivery."FailedAtUtc" IS NULL
                  AND delivery."Attempts" < {{MaxAttempts}}
                  AND delivery."AvailableAtUtc" <= {{now}}
                  AND (delivery."LockedUntilUtc" IS NULL OR delivery."LockedUntilUtc" <= {{now}})
                  AND NOT EXISTS (
                      SELECT 1
                      FROM "DailyStatisticsReportDeliveries" AS earlier
                      WHERE earlier."DailyStatisticsId" = delivery."DailyStatisticsId"
                        AND earlier."Mode" < delivery."Mode"
                        AND earlier."SentAtUtc" IS NULL
                        AND earlier."CancelledAtUtc" IS NULL
                        AND earlier."FailedAtUtc" IS NULL
                  )
                ORDER BY delivery."AvailableAtUtc", delivery."CreatedAtUtc",
                         delivery."DailyStatisticsId", delivery."Mode"
                LIMIT 1
                FOR UPDATE SKIP LOCKED
                """)
            .AsTracking()
            .SingleOrDefaultAsync(cancellationToken);

        if (claimed is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        claimed.Attempts++;
        claimed.LockedBy = _workerId;
        claimed.LockedUntilUtc = now.Add(LeaseDuration);

        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new DeliveryKey(claimed.DailyStatisticsId, claimed.Mode);
    }

    private async Task ProcessDeliveryAsync(DeliveryKey key, CancellationToken cancellationToken)
    {
        try
        {
            await SendDeliveryAsync(key, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Keep the lease on shutdown so another replica waits before making a
            // conservative retry of a potentially in-flight Telegram request.
            throw;
        }
        catch (Exception exception)
        {
            await RecordDeliveryFailureAsync(key, exception, cancellationToken);
        }
    }

    private async Task SendDeliveryAsync(DeliveryKey key, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        BotContext database = scope.ServiceProvider.GetRequiredService<BotContext>();
        DailyStatisticsReportDelivery? delivery = await database.DailyStatisticsReportDeliveries
            .AsNoTracking()
            .AsSplitQuery()
            .Include(item => item.DailyStatistics)
                .ThenInclude(statistic => statistic.ActiveUsers)
            .Include(item => item.DailyStatistics)
                .ThenInclude(statistic => statistic.Scores)
            .SingleOrDefaultAsync(
                item => item.DailyStatisticsId == key.DailyStatisticsId && item.Mode == key.Mode,
                cancellationToken);

        if (delivery is null ||
            IsTerminal(delivery) ||
            delivery.LockedBy != _workerId ||
            delivery.LockedUntilUtc is null ||
            delivery.LockedUntilUtc <= DateTimeOffset.UtcNow)
            return;

        if (!await TryRenewLeaseAsync(key, cancellationToken))
        {
            logger.LogWarning(
                "Skipped {Mode} daily statistic {DailyStatisticsId}: lease ownership was lost before report construction",
                delivery.Mode,
                delivery.DailyStatisticsId);
            return;
        }

        using CancellationTokenSource operationCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task leaseHeartbeat = MaintainLeaseAsync(key, operationCancellation);
        var telegramAccepted = false;

        try
        {
            ScoreHelper scoreHelper = scope.ServiceProvider.GetRequiredService<ScoreHelper>();
            BanchoApiV2 osuApi = scope.ServiceProvider.GetRequiredService<BanchoApiV2>();
            string text = await scoreHelper.GetDailyStatisticsSendText(
                delivery.Mode,
                delivery.DailyStatistics,
                osuApi);

            if (operationCancellation.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(
                    "Skipped {Mode} daily statistic {DailyStatisticsId}: lease ownership was lost during report construction",
                    delivery.Mode,
                    delivery.DailyStatisticsId);
                return;
            }

            operationCancellation.Token.ThrowIfCancellationRequested();
            if (!await TryRenewLeaseAsync(key, operationCancellation.Token))
            {
                operationCancellation.Cancel();
                logger.LogWarning(
                    "Skipped {Mode} daily statistic {DailyStatisticsId}: lease ownership was lost before send",
                    delivery.Mode,
                    delivery.DailyStatisticsId);
                return;
            }

            ITelegramBotClient botClient = scope.ServiceProvider.GetRequiredService<ITelegramBotClient>();
            await botClient.SendMessage(
                delivery.ChatId,
                text,
                ParseMode.Html,
                linkPreviewOptions: LinkPreviewOptions.Disabled,
                cancellationToken: operationCancellation.Token);
            telegramAccepted = true;
            await Task.Delay(PerChatSendInterval, operationCancellation.Token);
        }
        finally
        {
            operationCancellation.Cancel();
            await leaseHeartbeat;
        }

        if (!telegramAccepted)
            return;

        if (await TryMarkSentAsync(key, cancellationToken))
        {
            logger.LogInformation(
                "Delivered {Mode} daily statistic {DailyStatisticsId} to admin chat {ChatId} on attempt {Attempt}",
                delivery.Mode,
                delivery.DailyStatisticsId,
                delivery.ChatId,
                delivery.Attempts);
        }
        else
        {
            // Telegram may already have accepted the message. Leaving the row
            // non-terminal preserves at-least-once delivery semantics.
            logger.LogWarning(
                "Telegram accepted {Mode} daily statistic {DailyStatisticsId}, but worker {WorkerId} no longer owns the lease",
                delivery.Mode,
                delivery.DailyStatisticsId,
                _workerId);
        }
    }

    private async Task RecordDeliveryFailureAsync(
        DeliveryKey key,
        Exception exception,
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        BotContext database = scope.ServiceProvider.GetRequiredService<BotContext>();
        DailyStatisticsReportDelivery? delivery = await database.DailyStatisticsReportDeliveries
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item =>
                    item.DailyStatisticsId == key.DailyStatisticsId &&
                    item.Mode == key.Mode &&
                    item.SentAtUtc == null &&
                    item.CancelledAtUtc == null &&
                    item.FailedAtUtc == null &&
                    item.LockedBy == _workerId,
                cancellationToken);

        if (delivery is null)
            return;

        DateTimeOffset now = DateTimeOffset.UtcNow;
        string lastError = TruncateError(exception);
        int updated;

        if (delivery.Attempts >= MaxAttempts)
        {
            updated = await OwnedActiveDelivery(database, key, now)
                .Where(item => item.Attempts == delivery.Attempts)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.FailedAtUtc, _ => now)
                    .SetProperty(item => item.LastError, _ => lastError)
                    .SetProperty(item => item.LockedUntilUtc, _ => null)
                    .SetProperty(item => item.LockedBy, _ => null), cancellationToken);

            if (updated > 0)
            {
                logger.LogError(
                    exception,
                    "{Mode} daily statistic {DailyStatisticsId} permanently failed after {AttemptCount} attempts",
                    delivery.Mode,
                    delivery.DailyStatisticsId,
                    delivery.Attempts);
            }
        }
        else
        {
            TimeSpan retryDelay = GetRetryDelay(delivery.Attempts);
            DateTimeOffset availableAt = now.Add(retryDelay);
            updated = await OwnedActiveDelivery(database, key, now)
                .Where(item => item.Attempts == delivery.Attempts)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.AvailableAtUtc, _ => availableAt)
                    .SetProperty(item => item.LastError, _ => lastError)
                    .SetProperty(item => item.LockedUntilUtc, _ => null)
                    .SetProperty(item => item.LockedBy, _ => null), cancellationToken);

            if (updated > 0)
            {
                logger.LogWarning(
                    exception,
                    "{Mode} daily statistic {DailyStatisticsId} failed on attempt {AttemptCount}; retrying in {RetryDelay}",
                    delivery.Mode,
                    delivery.DailyStatisticsId,
                    delivery.Attempts,
                    retryDelay);
            }
        }

        if (updated == 0)
        {
            logger.LogWarning(
                "Ignored stale failure for {Mode} daily statistic {DailyStatisticsId}: worker {WorkerId} no longer owns an active lease",
                key.Mode,
                key.DailyStatisticsId,
                _workerId);
        }
    }

    private async Task MaintainLeaseAsync(
        DeliveryKey key,
        CancellationTokenSource operationCancellation)
    {
        CancellationToken cancellationToken = operationCancellation.Token;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(LeaseRenewalInterval, cancellationToken);

                if (await TryRenewLeaseAsync(key, cancellationToken))
                    continue;

                logger.LogWarning(
                    "Lost lease for {Mode} daily statistic {DailyStatisticsId}; cancelling report delivery",
                    key.Mode,
                    key.DailyStatisticsId);
                operationCancellation.Cancel();
                return;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal completion, host shutdown, or cancellation after lease loss.
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Could not renew lease for {Mode} daily statistic {DailyStatisticsId}; cancelling report delivery",
                key.Mode,
                key.DailyStatisticsId);
            operationCancellation.Cancel();
        }
    }

    private async Task<bool> TryRenewLeaseAsync(
        DeliveryKey key,
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        BotContext database = scope.ServiceProvider.GetRequiredService<BotContext>();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DateTimeOffset lockedUntil = now.Add(LeaseDuration);

        int updated = await OwnedActiveDelivery(database, key, now)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.LockedUntilUtc, _ => lockedUntil), cancellationToken);

        return updated == 1;
    }

    private async Task<bool> TryMarkSentAsync(
        DeliveryKey key,
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        BotContext database = scope.ServiceProvider.GetRequiredService<BotContext>();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        int updated = await OwnedActiveDelivery(database, key, now)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.SentAtUtc, _ => now)
                .SetProperty(item => item.LastError, _ => null)
                .SetProperty(item => item.LockedUntilUtc, _ => null)
                .SetProperty(item => item.LockedBy, _ => null), cancellationToken);

        return updated == 1;
    }

    private IQueryable<DailyStatisticsReportDelivery> OwnedActiveDelivery(
        BotContext database,
        DeliveryKey key,
        DateTimeOffset now) =>
        database.DailyStatisticsReportDeliveries.Where(item =>
            item.DailyStatisticsId == key.DailyStatisticsId &&
            item.Mode == key.Mode &&
            item.SentAtUtc == null &&
            item.CancelledAtUtc == null &&
            item.FailedAtUtc == null &&
            item.LockedBy == _workerId &&
            item.LockedUntilUtc != null &&
            item.LockedUntilUtc > now);

    private static TimeSpan GetRetryDelay(int attempt)
    {
        double seconds = Math.Min(300, 5 * Math.Pow(2, Math.Max(0, attempt - 1)));
        return TimeSpan.FromSeconds(seconds);
    }

    private static string TruncateError(Exception exception)
    {
        string value = $"{exception.GetType().Name}: {exception.Message}";
        return value.Length <= 2000 ? value : value[..2000];
    }

    private static bool IsTerminal(DailyStatisticsReportDelivery delivery) =>
        delivery.SentAtUtc is not null ||
        delivery.CancelledAtUtc is not null ||
        delivery.FailedAtUtc is not null;

    private readonly record struct DeliveryKey(int DailyStatisticsId, Playmode Mode);
}
