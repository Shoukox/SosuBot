using Microsoft.Extensions.Logging;
using OsuApi.V2;
using SosuBot.Database;
using SosuBot.Services.Data;
using Telegram.Bot;

namespace SosuBot.Services.Handlers.Abstract;

public interface ICommandContext<TUpdateType> where TUpdateType : class
{
    public ITelegramBotClient BotClient { get; }
    public TUpdateType Update { get; }
    public BotContext Database { get; }
    public ApiV2 OsuApiV2 { get; }
    public RabbitMqService RabbitMqService{ get; }
    public ILogger<TUpdateType> Logger { get; }
    public CancellationToken CancellationToken { get; }
}