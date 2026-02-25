using SosuBot.Extensions;
using SosuBot.Localization;
using SosuBot.TelegramHandlers.Abstract;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace SosuBot.TelegramHandlers.Commands;

public sealed class LanguageCommand : CommandBase<Message>
{
    public static readonly string[] Commands = ["/lang"];

    public override async Task ExecuteAsync()
    {
        var language = Context.GetLocalization();

        var ikm = new InlineKeyboardMarkup(
        [
            [InlineKeyboardButton.WithCallbackData(language.settings_language_ru, $"lang {Language.Russian}")],
            [InlineKeyboardButton.WithCallbackData(language.settings_language_en, $"lang {Language.English}")],
            [InlineKeyboardButton.WithCallbackData(language.settings_language_de, $"lang {Language.German}")]
        ]);

        await Context.Update.ReplyAsync(Context.BotClient, language.command_lang, replyMarkup: ikm);
    }
}
