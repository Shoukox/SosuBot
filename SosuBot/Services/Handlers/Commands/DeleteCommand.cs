using SosuBot.Database.Models;
using SosuBot.Services.Handlers.Abstract;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace SosuBot.Services.Handlers.Commands
{
    public class DeleteCommand : CommandBase<Message>
    {
        public static string[] Commands = ["/del"];

        public override async Task ExecuteAsync()
        {
            OsuUser? osuUserInDatabase = await Context.Database.OsuUsers.FindAsync(Context.Update.From!.Id);
            if (osuUserInDatabase is null || !osuUserInDatabase.IsAdmin) return;

            if (Context.Update.ReplyToMessage != null)
            {
                await Context.BotClient.DeleteMessage(Context.Update.ReplyToMessage.Chat.Id, Context.Update.ReplyToMessage.MessageId);
            }
        }
    }
}
