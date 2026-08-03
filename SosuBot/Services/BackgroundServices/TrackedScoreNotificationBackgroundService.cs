using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SosuBot.Database;
using SosuBot.Database.Models;
using SosuBot.Helpers;
using System.Globalization;
using System.Net;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace SosuBot.Services.BackgroundServices;

/// <summary>
/// Delivers score notifications created by SosuBot.ScoresObserver. Deliveries are
/// leased in PostgreSQL so several bot instances can process the queue safely.
/// </summary>
public sealed class TrackedScoreNotificationBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<TrackedScoreNotificationBackgroundService> logger) : BackgroundService
{
    // A single-item claim keeps the lease fresh until the Telegram request starts.
    // Multiple bot replicas still process different rows concurrently via SKIP LOCKED.
    private const int BatchSize = 1;
    private const int MaxAttempts = 5;
    private static readonly TimeSpan EmptyQueueDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ClaimFailureDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan LeaseRenewalInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan PerChatSendInterval = TimeSpan.FromMilliseconds(1100);

    private readonly string _workerId =
        $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Tracked score notification worker {WorkerId} started",
            _workerId);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                IReadOnlyList<DeliveryKey> deliveries = await ClaimPendingDeliveriesAsync(stoppingToken);
                if (deliveries.Count == 0)
                {
                    await Task.Delay(EmptyQueueDelay, stoppingToken);
                    continue;
                }

                foreach (DeliveryKey delivery in deliveries)
                {
                    stoppingToken.ThrowIfCancellationRequested();
                    await ProcessDeliveryAsync(delivery, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to claim tracked score notifications");
                await Task.Delay(ClaimFailureDelay, stoppingToken);
            }
        }

        logger.LogInformation(
            "Tracked score notification worker {WorkerId} stopped",
            _workerId);
    }

    private async Task<IReadOnlyList<DeliveryKey>> ClaimPendingDeliveriesAsync(
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        BotContext database = scope.ServiceProvider.GetRequiredService<BotContext>();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        // A process may have stopped during its final attempt. Once its lease expires,
        // close that delivery instead of sending it more than MaxAttempts times.
        await database.TrackedScoreDeliveries
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

        // CreatedAtUtc/ScoreId form the immutable per-chat FIFO order. An older
        // non-terminal row blocks newer rows even while it is leased or backing off.
        List<TrackedScoreDelivery> claimed = await database.TrackedScoreDeliveries
            .FromSqlInterpolated($$"""
                SELECT delivery.*
                FROM "TrackedScoreDeliveries" AS delivery
                WHERE delivery."SentAtUtc" IS NULL
                  AND delivery."CancelledAtUtc" IS NULL
                  AND delivery."FailedAtUtc" IS NULL
                  AND delivery."Attempts" < {{MaxAttempts}}
                  AND delivery."AvailableAtUtc" <= {{now}}
                  AND (delivery."LockedUntilUtc" IS NULL OR delivery."LockedUntilUtc" <= {{now}})
                  AND NOT EXISTS (
                      SELECT 1
                      FROM "TrackedScoreDeliveries" AS earlier
                      WHERE earlier."ChatId" = delivery."ChatId"
                        AND earlier."SentAtUtc" IS NULL
                        AND earlier."CancelledAtUtc" IS NULL
                        AND earlier."FailedAtUtc" IS NULL
                        AND (
                            earlier."CreatedAtUtc" < delivery."CreatedAtUtc"
                            OR (
                                earlier."CreatedAtUtc" = delivery."CreatedAtUtc"
                                AND earlier."ScoreId" < delivery."ScoreId"
                            )
                        )
                  )
                ORDER BY delivery."CreatedAtUtc", delivery."ScoreId", delivery."ChatId"
                LIMIT {{BatchSize}}
                FOR UPDATE SKIP LOCKED
                """)
            .AsTracking()
            .ToListAsync(cancellationToken);

        DateTimeOffset lockedUntil = now.Add(LeaseDuration);
        foreach (TrackedScoreDelivery delivery in claimed)
        {
            delivery.Attempts++;
            delivery.LockedBy = _workerId;
            delivery.LockedUntilUtc = lockedUntil;
        }

        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return claimed
            .Select(delivery => new DeliveryKey(delivery.ScoreId, delivery.ChatId))
            .ToArray();
    }

    private async Task ProcessDeliveryAsync(DeliveryKey key, CancellationToken cancellationToken)
    {
        try
        {
            await SendDeliveryAsync(key, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Keep the lease: another worker will make a conservative retry after it
            // expires, rather than racing an in-flight Telegram request on shutdown.
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

        TrackedScoreDelivery? delivery = await database.TrackedScoreDeliveries
            .AsNoTracking()
            .Include(item => item.Event)
            .SingleOrDefaultAsync(
                item => item.ScoreId == key.ScoreId && item.ChatId == key.ChatId,
                cancellationToken);

        if (delivery is null ||
            IsTerminal(delivery) ||
            delivery.LockedBy != _workerId ||
            delivery.LockedUntilUtc is null ||
            delivery.LockedUntilUtc <= DateTimeOffset.UtcNow)
            return;

        TrackedScoreEvent scoreEvent = delivery.Event;
        TelegramChat? chat = await database.TelegramChats
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.ChatId == delivery.ChatId, cancellationToken);

        if (!delivery.IsAdminRecipient &&
            (chat?.TrackedPlayers is null || !chat.TrackedPlayers.Contains(scoreEvent.PlayerId)))
        {
            if (await TryMarkCancelledAsync(key, cancellationToken))
            {
                logger.LogInformation(
                    "Cancelled score {ScoreId} delivery to chat {ChatId}: the player is no longer tracked",
                    delivery.ScoreId,
                    delivery.ChatId);
            }

            return;
        }

        ITelegramBotClient botClient = scope.ServiceProvider.GetRequiredService<ITelegramBotClient>();
        ScoreHelper scoreHelper = scope.ServiceProvider.GetRequiredService<ScoreHelper>();
        string text = BuildNotificationText(scoreEvent, scoreHelper);

        if (!await TryRenewLeaseAsync(key, cancellationToken))
        {
            logger.LogWarning(
                "Skipped tracked score {ScoreId} delivery to chat {ChatId}: lease ownership was lost before send",
                delivery.ScoreId,
                delivery.ChatId);
            return;
        }

        using CancellationTokenSource sendCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task leaseHeartbeat = MaintainLeaseAsync(key, sendCancellation);

        try
        {
            await botClient.SendMessage(
                delivery.ChatId,
                text,
                ParseMode.Html,
                linkPreviewOptions: LinkPreviewOptions.Disabled,
                cancellationToken: sendCancellation.Token);
            await Task.Delay(PerChatSendInterval, sendCancellation.Token);
        }
        finally
        {
            sendCancellation.Cancel();
            await leaseHeartbeat;
        }

        if (await TryMarkSentAsync(key, cancellationToken))
        {
            logger.LogInformation(
                "Delivered tracked score {ScoreId} to chat {ChatId} on attempt {Attempt}",
                delivery.ScoreId,
                delivery.ChatId,
                delivery.Attempts);
        }
        else
        {
            // Telegram may already have accepted the message. Leaving the row non-terminal
            // preserves at-least-once delivery and allows the current lease owner to retry.
            logger.LogWarning(
                "Telegram accepted tracked score {ScoreId} for chat {ChatId}, but worker {WorkerId} no longer owns the lease",
                delivery.ScoreId,
                delivery.ChatId,
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
        TrackedScoreDelivery? delivery = await database.TrackedScoreDeliveries
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item =>
                    item.ScoreId == key.ScoreId &&
                    item.ChatId == key.ChatId &&
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
                    "Tracked score {ScoreId} delivery to chat {ChatId} permanently failed after {AttemptCount} attempts",
                    delivery.ScoreId,
                    delivery.ChatId,
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
                    "Tracked score {ScoreId} delivery to chat {ChatId} failed on attempt {AttemptCount}; retrying in {RetryDelay}",
                    delivery.ScoreId,
                    delivery.ChatId,
                    delivery.Attempts,
                    retryDelay);
            }
        }

        if (updated == 0)
        {
            logger.LogWarning(
                "Ignored stale failure result for tracked score {ScoreId} and chat {ChatId}: worker {WorkerId} no longer owns an active lease",
                key.ScoreId,
                key.ChatId,
                _workerId);
        }
    }

    private async Task MaintainLeaseAsync(
        DeliveryKey key,
        CancellationTokenSource sendCancellation)
    {
        CancellationToken cancellationToken = sendCancellation.Token;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(LeaseRenewalInterval, cancellationToken);

                if (await TryRenewLeaseAsync(key, cancellationToken))
                    continue;

                logger.LogWarning(
                    "Lost lease for tracked score {ScoreId} and chat {ChatId} while sending; cancelling the in-flight request",
                    key.ScoreId,
                    key.ChatId);
                sendCancellation.Cancel();
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
                "Could not renew lease for tracked score {ScoreId} and chat {ChatId}; cancelling the in-flight request",
                key.ScoreId,
                key.ChatId);
            sendCancellation.Cancel();
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

    private async Task<bool> TryMarkCancelledAsync(
        DeliveryKey key,
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        BotContext database = scope.ServiceProvider.GetRequiredService<BotContext>();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        int updated = await OwnedActiveDelivery(database, key, now)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.CancelledAtUtc, _ => now)
                .SetProperty(item => item.LockedUntilUtc, _ => null)
                .SetProperty(item => item.LockedBy, _ => null), cancellationToken);

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

    private IQueryable<TrackedScoreDelivery> OwnedActiveDelivery(
        BotContext database,
        DeliveryKey key,
        DateTimeOffset now) =>
        database.TrackedScoreDeliveries.Where(item =>
            item.ScoreId == key.ScoreId &&
            item.ChatId == key.ChatId &&
            item.SentAtUtc == null &&
            item.CancelledAtUtc == null &&
            item.FailedAtUtc == null &&
            item.LockedBy == _workerId &&
            item.LockedUntilUtc != null &&
            item.LockedUntilUtc > now);

    private static string BuildNotificationText(TrackedScoreEvent scoreEvent, ScoreHelper scoreHelper)
    {
        var score = scoreEvent.ScoreJson;
        long scoreId = score.Id ?? scoreEvent.ScoreId;
        string username = WebUtility.HtmlEncode(
            score.User?.Username ?? $"osu! user {scoreEvent.PlayerId}");
        string pp = score.Pp is { } value
            ? value.ToString("0.##", CultureInfo.InvariantCulture)
            : "?";

        return $"<b>{username}</b> set a <b>{pp}pp</b> " +
               scoreHelper.GetScoreUrlWrappedInString(scoreId, "score!");
    }

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

    private static bool IsTerminal(TrackedScoreDelivery delivery) =>
        delivery.SentAtUtc is not null ||
        delivery.CancelledAtUtc is not null ||
        delivery.FailedAtUtc is not null;

    private readonly record struct DeliveryKey(long ScoreId, long ChatId);
}
