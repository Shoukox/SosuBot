using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SosuBot.Services.Data;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace SosuBot.Services.BackgroundServices;

public sealed class PollingBackgroundService(
    ILogger<PollingBackgroundService> logger,
    ITelegramBotClient botClient,
    UpdateQueueService updateQueueService) : BackgroundService
{
    private int? _offset = null;
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting polling background service");

        // Skip pending updates
        Update[] pendingUpdates = await botClient.GetUpdates(cancellationToken: stoppingToken);
        if(pendingUpdates.Length != 0)
        {
            _offset = pendingUpdates.Last().Id + 1;
        }

        // Start polling
        await EnqueueAllUpdates(stoppingToken);
    }

    private async Task EnqueueAllUpdates(CancellationToken stoppingToken)
    {
        logger.LogInformation("Bot is ready");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var updates = await botClient.GetUpdates(_offset, timeout: 30, cancellationToken: stoppingToken);
                if (updates.Length == 0)
                {
                    continue;
                }

                _offset = updates.Last().Id + 1;
                foreach (var update in updates)
                {
                    await updateQueueService.EnqueueUpdateAsync(update, stoppingToken);
                }
            }
            catch (OperationCanceledException e)
            {
                logger.LogWarning(e, "Operation cancelled");
                return;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Exception");
            }
        }
        logger.LogWarning("Finished its work");
    }
}