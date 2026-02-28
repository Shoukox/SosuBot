using Microsoft.Extensions.DependencyInjection;
using SosuBot.Database;
using SosuBot.Extensions;
using SosuBot.Localization;
using SosuBot.Localization.Languages;
using SosuBot.TelegramHandlers.Abstract;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace SosuBot.TelegramHandlers.Callbacks;

public sealed class LanguageCallback : CommandBase<CallbackQuery>
{
    public static readonly string Command = "lang";

    private BotContext _database = null!;

    public override async Task BeforeExecuteAsync()
    {
        await base.BeforeExecuteAsync();
        _database = Context.ServiceProvider.GetRequiredService<BotContext>();
    }

    public override async Task ExecuteAsync()
    {
        var parameters = Context.Update.Data!.Split(' ');
        var selectedLanguage = parameters.Length >= 2 ? parameters[1] : Language.Russian;

        var chatId = Context.Update.Message?.Chat.Id;
        if (chatId is null)
        {
            await Context.Update.AnswerAsync(Context.BotClient);
            return;
        }

        var chat = await _database.TelegramChats.FindAsync(chatId.Value);
        if (chat is null)
        {
            await Context.Update.AnswerAsync(Context.BotClient);
            return;
        }

        chat.LanguageCode = selectedLanguage switch
        {
            var value when value.StartsWith(Language.English, StringComparison.OrdinalIgnoreCase) => Language.English,
            var value when value.StartsWith(Language.German, StringComparison.OrdinalIgnoreCase) => Language.German,
            _ => Language.Russian
        };

        var selectedLocalization = chat.LanguageCode switch
        {
            Language.English => (ILocalization)new English(),
            Language.German => new Deutsch(),
            _ => new Russian()
        };

        await Context.Update.Message!.EditAsync(Context.BotClient, selectedLocalization.settings_language_changedSuccessfully);
        await Context.Update.AnswerAsync(Context.BotClient);
    }
}
