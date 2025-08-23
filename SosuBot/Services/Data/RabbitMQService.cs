using System.Text;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using SosuBot.Logging;

namespace SosuBot.Services.Data;

public class RabbitMqService
{
    private static readonly ILogger Logger = ApplicationLogging.CreateLogger(nameof(RabbitMqService));
    private IChannel? _channel;
    
    private static readonly object Locker = new object();

    public async Task Initialize()
    {
        var factory = new ConnectionFactory { HostName = "localhost" };
        var connection = await factory.CreateConnectionAsync();
        _channel = await connection.CreateChannelAsync();

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
            if(_channel == null) Initialize().GetAwaiter().GetResult();
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
        Logger.LogInformation("Job queued");
    }
}