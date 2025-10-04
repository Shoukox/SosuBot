using System.Text;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace SosuBot.Services;

public sealed class RabbitMqService(ILogger<RabbitMqService> logger)
{
    private static readonly object Locker = new();
    private IChannel? _channel;

    public async Task Initialize()
    {
        var factory = new ConnectionFactory { HostName = "localhost" };
        var connection = await factory.CreateConnectionAsync();
        _channel = await connection.CreateChannelAsync();

        await _channel.QueueDeclareAsync("task_queue", true, false,
            false, null);
    }

    /// <summary>
    ///     Queues a render job
    /// </summary>
    /// <param name="replayName">Only filename.extension</param>
    public async Task QueueJob(string replayName)
    {
        lock (Locker)
        {
            if (_channel == null) Initialize().GetAwaiter().GetResult();
        }

        var message = replayName;
        var body = Encoding.UTF8.GetBytes(message);

        var properties = new BasicProperties
        {
            Persistent = true
        };

        if (_channel == null) throw new Exception("Channel not initialized");
        await _channel.BasicPublishAsync(string.Empty, "render-job-queue", true,
            properties, body);
        logger.LogInformation("Job queued");
    }
}