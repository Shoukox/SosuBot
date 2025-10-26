using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SosuBot.Services.Handlers;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;

namespace SosuBot.Services.BackgroundServices;

public sealed class UpdateHandlerBackgroundService(
    UpdateQueueService updateQueue,
    IServiceProvider serviceProvider,
    ILogger<UpdateHandlerBackgroundService> logger)
    : BackgroundService
{
    private readonly int _workersCount = 100;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (EnableStressTestUsingConsole) _ = Task.Run(() => StressTestUsingConsole(stoppingToken));

        logger.LogInformation($"Starting {_workersCount} workers to handle updates.");

        try
        {
            var workers =
                Enumerable.Range(0, _workersCount)
                    .Select(_ => Task.Run(() => HandleUpdateWorker(stoppingToken)));

            await Task.WhenAll(workers);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Operation cancelled");
        }

        logger.LogInformation("Finished its work");
    }

    private async Task HandleUpdateWorker(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var update = await updateQueue.DequeueUpdateAsync(stoppingToken);

            await using var scope = serviceProvider.CreateAsyncScope();
            var updateHandler = scope.ServiceProvider.GetRequiredService<UpdateHandler>();
            var bot = scope.ServiceProvider.GetRequiredService<ITelegramBotClient>();

            try
            {
                await updateHandler.HandleUpdateAsync(bot, update, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                logger.LogWarning("Operation cancelled");
                break;
            }
            catch (Exception ex)
            {
                await updateHandler.HandleErrorAsync(bot, ex, HandleErrorSource.HandleUpdateError, stoppingToken);
            }
        }
    }

    #region stresstest

    public bool EnableStressTestUsingConsole = false;

    private async Task StressTestUsingConsole(CancellationToken stoppingToken, int messagesCount = 1000)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var line = Console.ReadLine();

            if (line == "gc")
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                logger.LogInformation("gc worked!");
                continue;
            }

            try
            {
                for (var i = 0; i < messagesCount; i++)
                    await updateQueue.EnqueueUpdateAsync(new Update
                    {
                        Id = Environment.TickCount,
                        Message = new Message
                        {
                            Text = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss.ffff")
                        }
                    }, stoppingToken);
                logger.LogInformation(messagesCount.ToString());
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    #endregion
}