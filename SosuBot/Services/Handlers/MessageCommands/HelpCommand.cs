using Sosu.Localization;
using SosuBot.Database;
using SosuBot.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace SosuBot.Services.Handlers.MessageCommands
{
    public class HelpCommand : CommandBase<Message>
    {
        public static string[] Commands = ["/help"];

        public override async Task ExecuteAsync()
        {
            ILocalization language = new Russian();
            await Context.ReplyAsync(BotClient, language.command_help);
        }
    }
}
