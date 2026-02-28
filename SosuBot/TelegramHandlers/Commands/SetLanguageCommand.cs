using SosuBot.Extensions;
using SosuBot.Localization;
using SosuBot.Localization.Languages;
using SosuBot.TelegramHandlers.Abstract;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace SosuBot.TelegramHandlers.Commands;

public sealed class SetLanguageCommand : CommandBase<Message>
{
    public static readonly string[] Commands = ["/botlang"];

    private static Russian _russian = new Russian();
    private static English _english = new English();
    private static Deutsch _deutsch = new Deutsch();

    public override async Task ExecuteAsync()
    {
        var language = Context.GetLocalization();

        if (Context.Update.Chat.Type is ChatType.Group or ChatType.Supergroup)
        {
            var chatAdmins = await Context.BotClient.GetChatAdministrators(Context.Update.Chat.Id);
            if (!chatAdmins.Any(m => m.User.Id == Context.Update.From?.Id))
            {
                await Context.Update.ReplyAsync(Context.BotClient, language.group_onlyForAdmins);
                return;
            }
        }

        var selectedLanguageCode = language.last_humanizerCulture switch
        {
            string culture when culture == _english.last_humanizerCulture => Language.English,
            string culture when culture == _deutsch.last_humanizerCulture => Language.German,
            string culture when culture == _russian.last_humanizerCulture => Language.Russian,
            _ => null
        };

        string MarkIfSelected(string text, string code) => selectedLanguageCode == code ? $"✅ {text}" : text;

        var ikm = new InlineKeyboardMarkup(
        [
            [InlineKeyboardButton.WithCallbackData(MarkIfSelected(language.settings_language_ru, Language.Russian), $"lang {Language.Russian}")],
            [InlineKeyboardButton.WithCallbackData(MarkIfSelected(language.settings_language_en, Language.English), $"lang {Language.English}")],
            [InlineKeyboardButton.WithCallbackData(MarkIfSelected(language.settings_language_de, Language.German), $"lang {Language.German}")]
        ]);

        await Context.Update.ReplyAsync(Context.BotClient, language.command_lang, replyMarkup: ikm);
    }
}
