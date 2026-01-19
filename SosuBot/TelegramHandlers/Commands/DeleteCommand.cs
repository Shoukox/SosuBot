using Microsoft.Extensions.DependencyInjection;
using SosuBot.Database;
using SosuBot.Extensions;
using SosuBot.TelegramHandlers.Abstract;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace SosuBot.TelegramHandlers.Commands;

public sealed class DeleteCommand : CommandBase<Message>
{
    private BotContext _database = null!;

    public static readonly string[] Commands = ["/del"];

    public override async Task BeforeExecuteAsync()
    {
        await base.BeforeExecuteAsync();
        _database = Context.ServiceProvider.GetRequiredService<BotContext>();
    }
    public override async Task ExecuteAsync()
    {
        var osuUserInDatabase = await _database.OsuUsers.FindAsync(Context.Update.From!.Id);
        if (osuUserInDatabase is null || !osuUserInDatabase.IsAdmin)
        {
            await Context.Update.ReplyAsync(Context.BotClient, "Пшол вон!");
            return;
        }

        if (Context.Update.ReplyToMessage != null)
            await Context.BotClient.DeleteMessage(Context.Update.ReplyToMessage.Chat.Id,
                Context.Update.ReplyToMessage.MessageId);
    }
}