using SosuBot.Extensions;
using SosuBot.Localization;
using SosuBot.Localization.Languages;
using SosuBot.Services.Handlers.Abstract;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace SosuBot.Services.Handlers.Commands;

public sealed class HelpCommand : CommandBase<Message>
{
    public static readonly string[] Commands = ["/help"];

    public override async Task ExecuteAsync()
    {
        ILocalization language = new Russian();

        bool isPrivateMessage = Context.Update.From?.Id == Context.Update.Chat.Id;
        if (!isPrivateMessage)
        {
            await Context.Update.ReplyAsync(Context.BotClient, "Отправлено в лс. Посмотрите в личку бота");
        }
        try
        {
            await Context.BotClient.SendMessage(Context.Update.From!.Id, language.command_help, Telegram.Bot.Types.Enums.ParseMode.Html);
        }
        catch { }
    }
}