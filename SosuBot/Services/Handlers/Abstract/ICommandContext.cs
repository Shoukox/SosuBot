using Microsoft.Extensions.Logging;
using OsuApi.Core.V2;
using SosuBot.Database;
using Telegram.Bot;

namespace SosuBot.Services.Handlers.Abstract
{
    public interface ICommandContext<TUpdateType> where TUpdateType : class
    {
        public ITelegramBotClient BotClient { get; }
        public TUpdateType Update { get; }
        public BotContext Database { get; }
        public ApiV2 OsuApiV2 { get; }
        public ILogger<ICommandContext<TUpdateType>> Logger { get; }
        public CancellationToken CancellationToken { get; }
    }
}
