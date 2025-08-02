using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace SosuBot.Helpers.OutputText;

public static class TelegramHelper
{
    public static string GetUserUrl(long userId)
    {
        return $"{TelegramConstants.BaseUserUrlWithUserId}{userId}";
    }

    public static string GetUserUrlWrappedInString(long userId, string text)
    {
        return $"<a href=\"{GetUserUrl(userId)}\">{text}</a>";
    }

    public static string GetUserFullName(User telegramUser)
    {
        return $"{telegramUser.FirstName} {telegramUser.LastName}";
    }

    public static async Task<Message> SendMessageConsideringTelegramLength(int messageId, long chatId,
        ITelegramBotClient botClient, string text,
        ParseMode parseMode = ParseMode.Html, InlineKeyboardMarkup? replyMarkup = null, bool edit = false)
    {
        var messagesToBeSent = text.Length / TelegramConstants.MaximumMessageLength +
                               Math.Sign(text.Length % TelegramConstants.MaximumMessageLength);
        Message returnMessage = null!;
        for (var i = 0; i < messagesToBeSent; i++)
        {
            var skipLength = i * TelegramConstants.MaximumMessageLength;
            var textPart = text.Substring(skipLength,
                Math.Min(text.Length - skipLength, TelegramConstants.MaximumMessageLength));

            if (i == 0 && edit)
                returnMessage = await botClient.EditMessageText(chatId, messageId, textPart,
                    parseMode,
                    linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true }, replyMarkup: replyMarkup);
            else
                returnMessage = await botClient.SendMessage(chatId, textPart,
                    parseMode,
                    linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true },
                    replyParameters: messageId,
                    replyMarkup: replyMarkup);
        }

        return returnMessage;
    }
}