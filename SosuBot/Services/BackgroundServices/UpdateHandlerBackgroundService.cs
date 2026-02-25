using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SosuBot.TelegramHandlers;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;

namespace SosuBot.Services.BackgroundServices;

public sealed class UpdateHandlerBackgroundService(IServiceProvider serviceProvider)
    : BackgroundService
{
    private readonly UpdateQueueService _updateQueue = serviceProvider.GetRequiredService<UpdateQueueService>();
    private readonly ILogger<UpdateHandlerBackgroundService> _logger = serviceProvider.GetRequiredService<ILogger<UpdateHandlerBackgroundService>>();
    private readonly int _workersCount = 128;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (EnableStressTestUsingConsole) _ = Task.Run(() => StressTestUsingConsole(stoppingToken));

        _logger.LogInformation($"Starting {_workersCount} workers to handle updates.");

        ThreadPool.GetAvailableThreads(out int workerThreads, out int completionPortThreads);
        _logger.LogInformation($"Available worker threads: {workerThreads}, available completion port threads: {completionPortThreads}");

        try
        {
            var workers =
                Enumerable.Range(0, _workersCount)
                    .Select(_ => Task.Run(() => HandleUpdateWorker(stoppingToken), stoppingToken));

            await Task.WhenAll(workers);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Operation cancelled");
        }

        _logger.LogInformation("Finished its work");
    }

    private async Task HandleUpdateWorker(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var update = await _updateQueue.DequeueUpdateAsync(stoppingToken);
                _logger.LogInformation("Worker dequeueing an update.");

                await using var scope = serviceProvider.CreateAsyncScope();
                var updateHandler = scope.ServiceProvider.GetRequiredService<UpdateHandler>();
                var bot = scope.ServiceProvider.GetRequiredService<ITelegramBotClient>();

                try
                {
                    await updateHandler.HandleUpdateAsync(bot, update, stoppingToken);
                }
                catch (Exception ex)
                {
                    await updateHandler.HandleErrorAsync(bot, ex, HandleErrorSource.HandleUpdateError, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in update handler worker");
                continue;
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
                _logger.LogInformation("gc worked!");
                continue;
            }

            try
            {
                for (var i = 0; i < messagesCount; i++)
                    await _updateQueue.EnqueueUpdateAsync(new Update
                    {
                        Id = Environment.TickCount,
                        Message = new Message
                        {
                            Text = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss.ffff")
                        }
                    }, stoppingToken);
                _logger.LogInformation(messagesCount.ToString());
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    #endregion
}