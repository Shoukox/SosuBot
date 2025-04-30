using Sosu.Localization;
using SosuBot.Extensions;
using Telegram.Bot.Types;

namespace SosuBot.Services.Handlers.Commands
{
    public class StartCommand : CommandBase<Message>
    {
        public static string[] Commands = ["/start"];

        public override async Task ExecuteAsync()
        {
            ILocalization language = new Russian();
            await Context.ReplyAsync(BotClient, language.command_start);
        }
    }
}
