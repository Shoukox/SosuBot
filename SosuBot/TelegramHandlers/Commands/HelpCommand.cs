using SosuBot.Extensions;
using SosuBot.TelegramHandlers.Abstract;
using Telegram.Bot.Types;

namespace SosuBot.TelegramHandlers.Commands;

public sealed class HelpCommand : CommandBase<Message>
{
    public static readonly string[] Commands = ["/help"];

    public override async Task ExecuteAsync()
    {
        var language = Context.GetLocalization();
        await Context.Update.ReplyAsync(Context.BotClient, language.command_help, parseMode: Telegram.Bot.Types.Enums.ParseMode.Html);
    }
}


