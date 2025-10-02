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
    public IServiceProvider ServiceProvider { get; }
    public CancellationToken CancellationToken { get; }
}