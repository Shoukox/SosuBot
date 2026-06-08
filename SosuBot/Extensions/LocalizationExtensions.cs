using Microsoft.Extensions.DependencyInjection;
using SosuBot.Database;
using SosuBot.Database.Models;
using SosuBot.Localization;
using SosuBot.Localization.Languages;
using SosuBot.TelegramHandlers.Abstract;
using Telegram.Bot.Types;

namespace SosuBot.Extensions;

public static class LocalizationExtensions
{
    private static readonly ILocalization EnglishLocalization = new English();
    private static readonly ILocalization GermanLocalization = new Deutsch();
    private static readonly ILocalization RussianLocalization = new Russian();

    public static ILocalization GetLocalization<TUpdateType>(this ICommandContext<TUpdateType> context) where TUpdateType : class
    {
        BotContext database = context.ServiceProvider.GetRequiredService<BotContext>();

        var chatId = context.Update switch
        {
            Message message => message.Chat.Id,
            CallbackQuery callbackQuery => callbackQuery.Message?.Chat.Id,
            _ => null
        };

        if (chatId is null)
            return RussianLocalization;

        TelegramChat? chat = database.TelegramChats.Find(chatId.Value);
        if (chat?.LanguageCode?.StartsWith(Language.English, StringComparison.OrdinalIgnoreCase) == true)
            return EnglishLocalization;

        if (chat?.LanguageCode?.StartsWith(Language.German, StringComparison.OrdinalIgnoreCase) == true)
            return GermanLocalization;

        return RussianLocalization;
    }
}
