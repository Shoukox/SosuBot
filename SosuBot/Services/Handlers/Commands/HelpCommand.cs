using SosuBot.Extensions;
using SosuBot.Localization;
using SosuBot.Services.Handlers.Abstract;
using Telegram.Bot.Types;

namespace SosuBot.Services.Handlers.Commands
{
    public class HelpCommand : CommandBase<Message>
    {
        public static string[] Commands = ["/help"];

        public override async Task ExecuteAsync()
        {
            ILocalization language = new Russian();
            await Context.Update.ReplyAsync(Context.BotClient, language.command_help);
        }
    }
}
