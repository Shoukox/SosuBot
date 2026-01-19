using SosuBot.Extensions;
using SosuBot.Localization;
using SosuBot.Localization.Languages;
using SosuBot.TelegramHandlers.Abstract;
using Telegram.Bot.Types;

namespace SosuBot.TelegramHandlers.Commands;

public sealed class StartCommand : CommandBase<Message>
{
    public static string[] Commands = ["/start"];

    public override async Task ExecuteAsync()
    {
        ILocalization language = new Russian();
        await Context.Update.ReplyAsync(Context.BotClient, language.command_start);
    }
}