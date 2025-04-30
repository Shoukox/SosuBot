using Microsoft.Extensions.Logging;
using OsuApi.Core.V2;
using SosuBot.Database;
using Telegram.Bot;

namespace SosuBot.Services.Handlers.Commands
{
    public class CommandBase<TUpdateType>
    {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        public ITelegramBotClient BotClient { get; private set; }
        public TUpdateType Context { get; private set; }
        public BotContext Database { get; private set; }
        public ApiV2 OsuApiV2 { get; private set; }
        public ILogger<CommandBase<TUpdateType>> Logger;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

        public void SetBotClient(ITelegramBotClient botClient) => BotClient = botClient;
        public void SetContext(TUpdateType context) => Context = context;
        public void SetDatabase(BotContext database) => Database = database;
        public void SetOsuApiV2(ApiV2 osuApiV2) => OsuApiV2 = osuApiV2;
        public void SetLogger(ILogger<CommandBase<TUpdateType>> logger) => Logger = logger;
        public virtual Task ExecuteAsync() => Task.CompletedTask;
    }
}
