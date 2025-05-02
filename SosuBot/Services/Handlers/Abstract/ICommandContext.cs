using Microsoft.Extensions.Logging;
using OsuApi.Core.V2;
using SosuBot.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types.Enums;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace SosuBot.Services.Handlers.Abstract
{
    public interface ICommandContext<TUpdateType> where TUpdateType : class
    {
        public ITelegramBotClient BotClient { get; }
        public TUpdateType Update { get; }
        public BotContext Database { get;  }
        public ApiV2 OsuApiV2 { get; }
        public ILogger<ICommandContext<TUpdateType>> Logger { get; }
    }
}
