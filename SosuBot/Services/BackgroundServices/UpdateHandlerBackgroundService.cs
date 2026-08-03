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
    private readonly int _workersCount = 64;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (EnableStressTestUsingConsole) _ = Task.Run(() => StressTestUsingConsole(stoppingToken));

        _logger.LogInformation("Starting {WorkerCount} workers to handle updates", _workersCount);

        ThreadPool.GetAvailableThreads(out int workerThreads, out int completionPortThreads);
        _logger.LogDebug(
            "Available worker threads: {WorkerThreads}, available completion port threads: {CompletionPortThreads}",
            workerThreads,
            completionPortThreads);

        try
        {
            IEnumerable<Task> workers =
                Enumerable.Range(0, _workersCount)
                    .Select(_ => Task.Run(() => HandleUpdateWorker(stoppingToken), stoppingToken));

            await Task.WhenAll(workers);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Update workers are stopping");
        }

        _logger.LogInformation("Finished its work");
    }

    private async Task HandleUpdateWorker(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                Update update = await _updateQueue.DequeueUpdateAsync(stoppingToken);
                _logger.LogDebug("Worker dequeued an update");

                await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();
                UpdateHandler updateHandler = scope.ServiceProvider.GetRequiredService<UpdateHandler>();
                ITelegramBotClient bot = scope.ServiceProvider.GetRequiredService<ITelegramBotClient>();

                try
                {
                    await updateHandler.HandleUpdateAsync(bot, update, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    await updateHandler.HandleErrorAsync(bot, ex, HandleErrorSource.HandleUpdateError, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
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
