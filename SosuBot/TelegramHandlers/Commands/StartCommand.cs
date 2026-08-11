using SosuBot.Extensions;
using SosuBot.Localization;
using SosuBot.TelegramHandlers.Abstract;
using Telegram.Bot.Types;

namespace SosuBot.TelegramHandlers.Commands;

public sealed class StartCommand : CommandBase<Message>
{
    public static string[] Commands = ["/start"];
    public static readonly string Description = "запустить бота";

    public override async Task ExecuteAsync()
    {
        ILocalization language = Context.GetLocalization();
        await Context.Update.ReplyAsync(Context.BotClient, language.command_start);
    }
}


