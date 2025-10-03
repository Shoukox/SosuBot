using SosuBot.Database;
using Telegram.Bot;

namespace SosuBot.Services.Handlers.Abstract;

public class CommandContext<TUpdateType>(
    ITelegramBotClient botClient,
    TUpdateType update,
    BotContext database,
    IServiceProvider serviceProvider,
    CancellationToken cancellationToken)
    : ICommandContext<TUpdateType>
    where TUpdateType : class
{
    public ITelegramBotClient BotClient { get; } = botClient;
    public TUpdateType Update { get; } = update;

    public BotContext Database { get; } = database;

    public IServiceProvider ServiceProvider { get; } = serviceProvider;
    public CancellationToken CancellationToken { get; } = cancellationToken;
}