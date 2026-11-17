using System.Threading.Channels;
using SosuBot.Monitoring;
using Telegram.Bot.Types;

namespace SosuBot.Services;

public sealed class UpdateQueueService(BotMetrics metrics)
{
    /// <summary>
    /// Updates waiting for a worker. When the workers fall behind, polling waits instead of buffering without
    /// limit; Telegram keeps the rest of the updates on its side until the next getUpdates.
    /// </summary>
    public const int Capacity = 1_000;

    private readonly Channel<Update> _channel = Channel.CreateBounded<Update>(new BoundedChannelOptions(Capacity)
    {
        FullMode = BoundedChannelFullMode.Wait
    });

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
