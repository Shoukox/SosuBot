using SosuBot.Extensions;
using SosuBot.Localization;
using SosuBot.Localization.Languages;
using SosuBot.Services.Handlers.Abstract;
using Telegram.Bot.Types;

namespace SosuBot.Services.Handlers.Commands;

public sealed class HelpCommand : CommandBase<Message>
{
    public static readonly string[] Commands = ["/help"];

    public override async Task ExecuteAsync()
    {
        ILocalization language = new Russian();
        await Context.Update.ReplyAsync(Context.BotClient, language.command_help, parseMode: Telegram.Bot.Types.Enums.ParseMode.Html);
    }
}