using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SosuBot.Logging;
using SosuBot.Services.Data;
using Telegram.Bot;

namespace SosuBot.Services.BackgroundServices;

public sealed class PollingBackgroundService(
    ITelegramBotClient botClient,
    UpdateQueueService updateQueueService) : BackgroundService
{
    private static readonly ILogger Logger = ApplicationLogging.CreateLogger(nameof(PollingBackgroundService));
    
    private int? _offset;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Logger.LogInformation("Starting polling background service");

        // Skip pending updates
        var pendingUpdates = await botClient.GetUpdates(cancellationToken: stoppingToken);
        if (pendingUpdates.Length != 0) _offset = pendingUpdates.Last().Id + 1;

        // Start polling
        await EnqueueAllUpdates(stoppingToken);
    }

    private async Task EnqueueAllUpdates(CancellationToken stoppingToken)
    {
        Logger.LogInformation("Bot is ready");
        while (!stoppingToken.IsCancellationRequested)
            try
            {
                var updates = await botClient.GetUpdates(_offset, timeout: 30, cancellationToken: stoppingToken);
                if (updates.Length == 0) continue;

                _offset = updates.Last().Id + 1;
                foreach (var update in updates) await updateQueueService.EnqueueUpdateAsync(update, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                Logger.LogWarning("Operation cancelled");
                return;
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Exception");
            }

        Logger.LogWarning("Finished its work");
    }
}