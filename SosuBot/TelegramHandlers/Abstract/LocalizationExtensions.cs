using SosuBot.Localization;
using SosuBot.Localization.Languages;
using Telegram.Bot.Types;

namespace SosuBot.TelegramHandlers.Abstract;

public static class LocalizationExtensions
{
    public static ILocalization GetLocalization<TUpdateType>(this ICommandContext<TUpdateType> context) where TUpdateType : class
    {
        var languageCode = context.Update switch
        {
            Message message => message.From?.LanguageCode,
            CallbackQuery callbackQuery => callbackQuery.From?.LanguageCode,
            _ => null
        };

        if (!string.IsNullOrWhiteSpace(languageCode) && languageCode.StartsWith(Language.English, StringComparison.OrdinalIgnoreCase))
            return new English();

        return new Russian();
    }
}

