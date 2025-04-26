using System.Threading.Channels;
using Telegram.Bot.Types;

namespace SosuBot.Services.Data;

public class UpdateQueueService()
{
    private readonly Channel<Update> _channel = Channel.CreateUnbounded<Update>();
    public async Task EnqueueUpdateAsync(Update update, CancellationToken stoppingToken)
    {
        await _channel.Writer.WriteAsync(update, stoppingToken);
    }

    public async Task<Update> DequeueUpdateAsync(CancellationToken stoppingToken)
    {
        return await _channel.Reader.ReadAsync(stoppingToken);
    }
}