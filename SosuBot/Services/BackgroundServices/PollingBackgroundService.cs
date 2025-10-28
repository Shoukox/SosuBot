using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;

namespace SosuBot.Services.BackgroundServices;

public sealed class PollingBackgroundService(IServiceProvider serviceProvider) : BackgroundService
{
    private readonly ITelegramBotClient _botClient = serviceProvider.GetRequiredService<ITelegramBotClient>();
    private readonly UpdateQueueService _updateQueueService = serviceProvider.GetRequiredService<UpdateQueueService>();
    private readonly ILogger<PollingBackgroundService> _logger = serviceProvider.GetRequiredService<ILogger<PollingBackgroundService>>();
    private int? _offset;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting polling background service");

        try
        {
            // Skip pending updates
            var pendingUpdates = await _botClient.GetUpdates();
            if (pendingUpdates.Length != 0) _offset = pendingUpdates.Last().Id + 1;
        }
        catch (OperationCanceledException e)
        {
            _logger.LogError(e, "Operation cancelled");
            return;
        }

        // Start polling
        await EnqueueAllUpdates(stoppingToken);
    }

    private async Task EnqueueAllUpdates(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Bot is ready");
        while (!stoppingToken.IsCancellationRequested)
            try
            {
                var updates = await _botClient.GetUpdates(_offset);
                if (updates.Length == 0) continue;

                _offset = updates.Last().Id + 1;
                foreach (var update in updates)
                    await _updateQueueService.EnqueueUpdateAsync(update, CancellationToken.None);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Exception");
            }

        _logger.LogInformation("Finished its work");
    }
}