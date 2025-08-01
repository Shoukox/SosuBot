using Microsoft.Extensions.Logging;
using OsuApi.V2;
using SosuBot.Database;
using Telegram.Bot;

namespace SosuBot.Services.Handlers.Abstract;

public class CommandContext<TUpdateType> : ICommandContext<TUpdateType> where TUpdateType : class
{
    public CommandContext(ITelegramBotClient botClient, TUpdateType update, BotContext database, ApiV2 osuApiV2,
        ILogger<ICommandContext<TUpdateType>> logger, CancellationToken cancellationToken)
    {
        BotClient = botClient;
        Update = update;
        Database = database;
        OsuApiV2 = osuApiV2;
        Logger = logger;
        CancellationToken = cancellationToken;
    }

    public ITelegramBotClient BotClient { get; }

    public TUpdateType Update { get; }

    public BotContext Database { get; }

    public ApiV2 OsuApiV2 { get; }

    public ILogger<ICommandContext<TUpdateType>> Logger { get; }
    public CancellationToken CancellationToken { get; }
}