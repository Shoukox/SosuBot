using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SosuBot.Database;

namespace SosuBot.Monitoring;

public sealed class CommandUsageRecorder(
    IServiceScopeFactory scopeFactory,
    BotMetrics metrics,
    ILogger<CommandUsageRecorder> logger)
{
    public async Task RecordAsync(
        string command,
        string status,
        TimeSpan duration,
        CancellationToken cancellationToken = default)
    {
        string normalizedCommand = NormalizeCommand(command);
        string normalizedStatus = NormalizeStatus(status);
        long durationMilliseconds = Math.Max(0, (long)duration.TotalMilliseconds);

        metrics.RecordCommandExecution(normalizedCommand, normalizedStatus, duration.TotalSeconds);

        DateTimeOffset now = DateTimeOffset.UtcNow;
        DateTimeOffset bucketStart = new(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, TimeSpan.Zero);
        long successCount = normalizedStatus == "success" ? 1 : 0;
        long errorCount = normalizedStatus == "error" ? 1 : 0;
        long cancelledCount = normalizedStatus == "cancelled" ? 1 : 0;

        try
        {
            TimeSpan persistenceTimeout = cancellationToken.IsCancellationRequested
                ? TimeSpan.FromMilliseconds(500)
                : TimeSpan.FromSeconds(2);
            using var timeout = new CancellationTokenSource(persistenceTimeout);
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            BotContext database = scope.ServiceProvider.GetRequiredService<BotContext>();
            await database.Database.ExecuteSqlInterpolatedAsync($$"""
                INSERT INTO "CommandUsageAggregates"
                    ("Command", "BucketStartUtc", "SuccessCount", "ErrorCount", "CancelledCount", "TotalDurationMilliseconds")
                VALUES
                    ({{normalizedCommand}}, {{bucketStart}}, {{successCount}}, {{errorCount}}, {{cancelledCount}}, {{durationMilliseconds}})
                ON CONFLICT ("Command", "BucketStartUtc") DO UPDATE SET
                    "SuccessCount" = "CommandUsageAggregates"."SuccessCount" + EXCLUDED."SuccessCount",
                    "ErrorCount" = "CommandUsageAggregates"."ErrorCount" + EXCLUDED."ErrorCount",
                    "CancelledCount" = "CommandUsageAggregates"."CancelledCount" + EXCLUDED."CancelledCount",
                    "TotalDurationMilliseconds" = "CommandUsageAggregates"."TotalDurationMilliseconds" + EXCLUDED."TotalDurationMilliseconds";
                """, timeout.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            metrics.RecordCommandPersistenceFailure();
            logger.LogDebug(
                "Could not persist usage statistics for command {Command} within the shutdown grace period",
                normalizedCommand);
        }
        catch (OperationCanceledException)
        {
            metrics.RecordCommandPersistenceFailure();
            logger.LogWarning("Timed out while persisting usage statistics for command {Command}", normalizedCommand);
        }
        catch (Exception exception)
        {
            metrics.RecordCommandPersistenceFailure();
            logger.LogWarning(exception, "Failed to persist usage statistics for command {Command}", normalizedCommand);
        }
    }

    private static string NormalizeCommand(string command)
    {
        string normalized = command.Trim().TrimStart('/').ToLowerInvariant();
        return string.IsNullOrWhiteSpace(normalized) ? "unknown" : normalized[..Math.Min(normalized.Length, 64)];
    }

    private static string NormalizeStatus(string status) => status switch
    {
        "success" => "success",
        "cancelled" => "cancelled",
        _ => "error"
    };
}
