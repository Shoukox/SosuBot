using Telegram.Bot;

namespace SosuBot.TelegramHandlers.Abstract;

public interface ICommandContext<TUpdateType> where TUpdateType : class
{
    public ITelegramBotClient BotClient { get; }
    public TUpdateType Update { get; }
    public IServiceProvider ServiceProvider { get; }
    public CancellationToken CancellationToken { get; }
}
