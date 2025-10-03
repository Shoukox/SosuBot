using SosuBot.Services.Handlers.Abstract;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace SosuBot.Services.Handlers.Commands;

public sealed class DeleteCommand : CommandBase<Message>
{
    public static readonly string[] Commands = ["/del"];

    public override async Task ExecuteAsync()
    {
        var osuUserInDatabase = await Context.Database.OsuUsers.FindAsync(Context.Update.From!.Id);
        if (osuUserInDatabase is null || !osuUserInDatabase.IsAdmin) return;

        if (Context.Update.ReplyToMessage != null)
            await Context.BotClient.DeleteMessage(Context.Update.ReplyToMessage.Chat.Id,
                Context.Update.ReplyToMessage.MessageId);
    }
}