using SosuBot.Caching;
using SosuBot.Database;
using Telegram.Bot;

namespace SosuBot.Services.Handlers.Abstract;

public class CommandContext<TUpdateType>(
    ITelegramBotClient botClient,
    TUpdateType update,
    BotContext database,
    IServiceProvider serviceProvider,
    RedisCaching redis,
    CancellationToken cancellationToken)
    : ICommandContext<TUpdateType>
    where TUpdateType : class
{
    public ITelegramBotClient BotClient { get; } = botClient;
    public TUpdateType Update { get; } = update;

    public BotContext Database { get; } = database;

    public IServiceProvider ServiceProvider { get; } = serviceProvider;
    public RedisCaching Redis { get; } = redis;
    public CancellationToken CancellationToken { get; } = cancellationToken;
}