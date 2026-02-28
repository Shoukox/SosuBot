using SosuBot.Extensions;
using SosuBot.Localization;
using SosuBot.TelegramHandlers.Abstract;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace SosuBot.TelegramHandlers.Commands;

public sealed class SetLanguageCommand : CommandBase<Message>
{
    public static readonly string[] Commands = ["/botlang"];

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

        var ikm = new InlineKeyboardMarkup(
        [
            [InlineKeyboardButton.WithCallbackData(language.settings_language_ru, $"lang {Language.Russian}")],
            [InlineKeyboardButton.WithCallbackData(language.settings_language_en, $"lang {Language.English}")],
            [InlineKeyboardButton.WithCallbackData(language.settings_language_de, $"lang {Language.German}")]
        ]);

        await Context.Update.ReplyAsync(Context.BotClient, language.command_lang, replyMarkup: ikm);
    }
}
