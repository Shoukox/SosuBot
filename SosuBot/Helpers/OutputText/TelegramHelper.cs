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

    private static async Task<Message> SendOrEditMessage(int messageId, long chatId,
        ITelegramBotClient botClient, string text, bool first, bool edit,
        ParseMode parseMode = ParseMode.Html, InlineKeyboardMarkup? replyMarkup = null)
    {
        if (first && edit)
            return await botClient.EditMessageText(chatId, messageId, text, parseMode,
                linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true }, replyMarkup: replyMarkup);

        return await botClient.SendMessage(chatId, text, parseMode,
            linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true }, replyParameters: messageId,
            replyMarkup: replyMarkup);
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

            returnMessage = await SendOrEditMessage(messageId, chatId, botClient, textPart, i == 0, edit, parseMode,
                replyMarkup: replyMarkup);
        }

        return returnMessage;
    }

    public static async Task<Message> SendMessageConsideringTelegramLengthAndSplitValue(int messageId, long chatId,
        ITelegramBotClient botClient, string text,
        ParseMode parseMode = ParseMode.Html, InlineKeyboardMarkup? replyMarkup = null, bool edit = false,
        string splitValue = "\n\n")
    {
        string[] splittedText = text.Split(splitValue);
        string currentText = "";
        for (int i = 0; i < splittedText.Length; i++)
        {
            if (i != splittedText.Length - 1 && currentText + splittedText[i] is string textPart &&
                textPart.Length > TelegramConstants.MaximumMessageLength)
            {
                await SendOrEditMessage(messageId, chatId, botClient, textPart, i == 0, edit, parseMode,
                    replyMarkup: replyMarkup);
                currentText = "";
            }

            currentText += splittedText[i] + "\n\n";
        }

        return await SendOrEditMessage(messageId, chatId, botClient, currentText, false, edit, parseMode,
            replyMarkup: replyMarkup);
    }
}