using Sosu.Localization;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace SosuBot.Services.Handlers.Commands.MessageCommands
{
    public class DeleteCommand : CommandBase<Message>
    {
        public static string[] Commands = ["/del"];

        public override async Task ExecuteAsync()
        {
            ILocalization language = new Russian();
            OsuUser? osuUserInDatabase = await Database.OsuUsers.FindAsync(Context.From!.Id);

            if (osuUserInDatabase is null || !osuUserInDatabase.IsAdmin) return;

            if (Context.ReplyToMessage != null)
            {
                await BotClient.DeleteMessage(Context.ReplyToMessage.Chat.Id, Context.ReplyToMessage.MessageId);
            }
        }
    }
}
