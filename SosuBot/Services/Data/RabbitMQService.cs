using System.Text;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace SosuBot.Services.Data;

public class RabbitMQService(ILogger<RabbitMQService> logger)
{
    private IChannel? _channel;

    private static readonly object Locker = new object();

    private ConnectionFactory? _factory;
    private IConnection? _connection;

    public async Task Initialize()
    {
        _factory = new ConnectionFactory { HostName = "localhost" };
        _connection = await _factory.CreateConnectionAsync();
        _channel = await _connection.CreateChannelAsync();

        await _channel.QueueDeclareAsync(queue: "task_queue", durable: true, exclusive: false,
            autoDelete: false, arguments: null);
    }

    /// <summary>
    /// Queues a render job
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

        if (_channel == null)
        {
            throw new Exception("Channel not initialized");
        }

        await _channel.BasicPublishAsync(exchange: string.Empty, routingKey: "render-job-queue", mandatory: true,
            basicProperties: properties, body: body);
        logger.LogInformation("Job queued");
    }
}