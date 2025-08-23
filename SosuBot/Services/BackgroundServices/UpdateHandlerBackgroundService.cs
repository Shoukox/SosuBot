using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SosuBot.Logging;
using SosuBot.Services.Data;
using SosuBot.Services.Handlers;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;

namespace SosuBot.Services.BackgroundServices;

public sealed class UpdateHandlerBackgroundService(
    UpdateQueueService updateQueue,
    IServiceProvider serviceProvider)
    : BackgroundService
{
    private static readonly ILogger Logger = ApplicationLogging.CreateLogger(nameof(UpdateHandlerBackgroundService));
    public int WorkersCount = 100;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (EnableStressTestUsingConsole) _ = Task.Run(() => StressTestUsingConsole(stoppingToken));

        Logger.LogInformation($"Starting {WorkersCount} workers to handle updates.");
        var workers =
            Enumerable.Range(0, WorkersCount)
                .Select(_ => Task.Run(() => HandleUpdateWorker(stoppingToken)));

        await Task.WhenAll(workers);
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
                Logger.LogWarning("Operation cancelled");
                return;
            }
            catch (Exception ex)
            {
                await updateHandler.HandleErrorAsync(bot, ex, HandleErrorSource.HandleUpdateError, stoppingToken);
            }
        }

        Logger.LogWarning("Finished its work");
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
                Logger.LogInformation("gc worked!");
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
                Logger.LogInformation(messagesCount.ToString());
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    #endregion
}