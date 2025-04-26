using OsuApi.Core.V2;
using SosuBot.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;

namespace SosuBot.Services.Handlers.MessageCommands
{
    public class CommandBase<TUpdateType>
    {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        public ITelegramBotClient BotClient { get; private set; }
        public TUpdateType Context { get; private set; }
        public BotContext Database { get; private set; }
        public ApiV2 OsuApiV2 { get; private set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

        public void SetBotClient(ITelegramBotClient botClient) => BotClient = botClient;
        public void SetContext(TUpdateType context) => Context = context;
        public void SetDatabase(BotContext database) => Database = database;
        public void SetOsuApiV2(ApiV2 osuApiV2) => OsuApiV2 = osuApiV2;
        public virtual Task ExecuteAsync() => Task.CompletedTask;
    }
}
