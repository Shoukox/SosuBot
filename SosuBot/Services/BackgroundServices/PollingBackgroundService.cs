using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;

namespace SosuBot.Services.BackgroundServices;

public sealed class PollingBackgroundService(
    ITelegramBotClient botClient,
    UpdateQueueService updateQueueService,
    ILogger<PollingBackgroundService> logger) : BackgroundService
{
    private int? _offset;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting polling background service");

        try
        {
            // Skip pending updates
            var pendingUpdates = await botClient.GetUpdates();
            if (pendingUpdates.Length != 0) _offset = pendingUpdates.Last().Id + 1;
        }
        catch (OperationCanceledException e)
        {
            logger.LogError(e, "Operation cancelled");
            return;
        }

        // Start polling
        await EnqueueAllUpdates(stoppingToken);
    }

    private async Task EnqueueAllUpdates(CancellationToken stoppingToken)
    {
        logger.LogInformation("Bot is ready");
        while (!stoppingToken.IsCancellationRequested)
            try
            {
                var updates = await botClient.GetUpdates(_offset);
                if (updates.Length == 0) continue;

                _offset = updates.Last().Id + 1;
                foreach (var update in updates)
                    await updateQueueService.EnqueueUpdateAsync(update, CancellationToken.None);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Exception");
            }

        logger.LogInformation("Finished its work");
    }
}