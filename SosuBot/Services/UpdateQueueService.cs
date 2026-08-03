using System.Threading.Channels;
using SosuBot.Monitoring;
using Telegram.Bot.Types;

namespace SosuBot.Services;

public sealed class UpdateQueueService(BotMetrics metrics)
{
    private readonly Channel<Update> _channel = Channel.CreateUnbounded<Update>();

    public async Task EnqueueUpdateAsync(Update update, CancellationToken stoppingToken)
    {
        metrics.UpdateQueued();
        try
        {
            await _channel.Writer.WriteAsync(update, stoppingToken);
        }
        catch
        {
            metrics.UpdateDequeued();
            throw;
        }
    }

    public async Task<Update> DequeueUpdateAsync(CancellationToken stoppingToken)
    {
        Update update = await _channel.Reader.ReadAsync(stoppingToken);
        metrics.UpdateDequeued();
        return update;
    }
}
