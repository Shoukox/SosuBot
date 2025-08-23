using Microsoft.Extensions.Logging;
using OsuApi.V2;
using SosuBot.Database;
using SosuBot.Services.Data;
using Telegram.Bot;

namespace SosuBot.Services.Handlers.Abstract;

public class CommandContext<TUpdateType> : ICommandContext<TUpdateType> where TUpdateType : class
{
    public CommandContext(ITelegramBotClient botClient, TUpdateType update, BotContext database, ApiV2 osuApiV2,
        RabbitMqService rabbitMqService, CancellationToken cancellationToken)
    {
        BotClient = botClient;
        Update = update;
        Database = database;
        OsuApiV2 = osuApiV2;
        RabbitMqService = rabbitMqService;
        CancellationToken = cancellationToken;
    }

    public ITelegramBotClient BotClient { get; }

    public TUpdateType Update { get; }

    public BotContext Database { get; }

    public ApiV2 OsuApiV2 { get; }

    public RabbitMqService RabbitMqService { get; }
    public CancellationToken CancellationToken { get; }
}