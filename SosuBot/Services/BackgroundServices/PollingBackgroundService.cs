using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace SosuBot.Services.BackgroundServices;

public sealed class PollingBackgroundService(IServiceProvider serviceProvider) : BackgroundService
{
    private readonly ITelegramBotClient _botClient = serviceProvider.GetRequiredService<ITelegramBotClient>();
    private readonly UpdateQueueService _updateQueueService = serviceProvider.GetRequiredService<UpdateQueueService>();
    private readonly ILogger<PollingBackgroundService> _logger = serviceProvider.GetRequiredService<ILogger<PollingBackgroundService>>();
    private int? _offset;
    private static readonly UpdateType[] AllowedUpdates = [
        UpdateType.Message,
        UpdateType.CallbackQuery,
        UpdateType.ChatMember
    ];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting polling background service");

        try
        {
            // Skip pending updates
            Update[] pendingUpdates = await _botClient.GetUpdates(
                timeout: 1,
                allowedUpdates: AllowedUpdates,
                cancellationToken: stoppingToken);
            foreach (Update pendingUpdate in pendingUpdates.Where(IsMembershipUpdate))
                await _updateQueueService.EnqueueUpdateAsync(pendingUpdate, stoppingToken);

            if (pendingUpdates.Length != 0)
                _offset = pendingUpdates.Last().Id + 1;
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Polling background service stopped during startup");
            return;
        }
        catch (TaskCanceledException exception) when (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "Timed out while inspecting pending Telegram updates; polling will retry");
        }
        catch (ApiRequestException exception)
        {
            _logger.LogWarning(
                "Failed to inspect pending Telegram updates with API error {ErrorCode}; polling will retry",
                exception.ErrorCode);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(
                "Failed to inspect pending Telegram updates with HTTP status {StatusCode}; polling will retry",
                exception.StatusCode);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to inspect pending Telegram updates; polling will retry");
        }

        // Start polling
        await EnqueueAllUpdates(stoppingToken);
    }

    private async Task EnqueueAllUpdates(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Bot is ready");
        int consecutiveFailures = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                Update[] updates = await _botClient.GetUpdates(
                    _offset,
                    timeout: 20,
                    allowedUpdates: AllowedUpdates,
                    cancellationToken: stoppingToken);
                consecutiveFailures = 0;
                _logger.LogDebug("Received {Count} updates", updates.Length);
                if (updates.Length == 0) continue;

                _offset = updates.Last().Id + 1;
                foreach (Update update in updates)
                    await _updateQueueService.EnqueueUpdateAsync(update, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (TaskCanceledException exception) when (!stoppingToken.IsCancellationRequested)
            {
                consecutiveFailures++;
                _logger.LogWarning(
                    exception,
                    "Telegram polling timed out; retry {FailureCount}",
                    consecutiveFailures);
                await DelayAfterFailure(consecutiveFailures, stoppingToken);
            }
            catch (ApiRequestException exception)
            {
                consecutiveFailures++;
                _logger.LogWarning(
                    "Telegram polling failed with API error {ErrorCode}; retry {FailureCount}",
                    exception.ErrorCode,
                    consecutiveFailures);
                await DelayAfterFailure(consecutiveFailures, stoppingToken);
            }
            catch (HttpRequestException exception)
            {
                consecutiveFailures++;
                _logger.LogWarning(
                    "Telegram polling failed with HTTP status {StatusCode}; retry {FailureCount}",
                    exception.StatusCode,
                    consecutiveFailures);
                await DelayAfterFailure(consecutiveFailures, stoppingToken);
            }
            catch (Exception exception)
            {
                consecutiveFailures++;
                _logger.LogError(exception, "Unexpected Telegram polling failure; retry {FailureCount}", consecutiveFailures);
                await DelayAfterFailure(consecutiveFailures, stoppingToken);
            }
        }

        _logger.LogInformation("Finished its work");
    }

    private static bool IsMembershipUpdate(Update update) =>
        update.ChatMember is not null ||
        update.Message?.LeftChatMember is not null ||
        update.Message?.NewChatMembers is { Length: > 0 };

    private static Task DelayAfterFailure(int consecutiveFailures, CancellationToken cancellationToken)
    {
        double exponentialDelaySeconds = Math.Pow(2, Math.Min(consecutiveFailures - 1, 5));
        return Task.Delay(TimeSpan.FromSeconds(exponentialDelaySeconds), cancellationToken);
    }
}
