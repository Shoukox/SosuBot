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
        var sentOrEdited = false;
        for (var i = 0; i < messagesToBeSent; i++)
        {
            var skipLength = i * TelegramConstants.MaximumMessageLength;

            var textPart = text.Substring(skipLength,
                Math.Min(text.Length - skipLength, TelegramConstants.MaximumMessageLength));

            returnMessage = await SendOrEditMessage(messageId, chatId, botClient, textPart, !sentOrEdited, edit,
                parseMode,
                replyMarkup);
            sentOrEdited = true;
        }

        return returnMessage;
    }

    public static async Task<Message> SendMessageConsideringTelegramLengthAndSplitValue(int messageId, long chatId,
        ITelegramBotClient botClient, string text,
        ParseMode parseMode = ParseMode.Html, InlineKeyboardMarkup? replyMarkup = null, bool edit = false,
        string splitValue = "\n\n")
    {
        var splittedText = text.Split(splitValue);
        var currentText = "";
        var sentOrEdited = false;
        for (var i = 0; i < splittedText.Length; i++)
        {
            if (i != splittedText.Length - 1 && currentText + splittedText[i] is
                    { Length: > TelegramConstants.MaximumMessageLength })
            {
                await SendOrEditMessage(messageId, chatId, botClient, currentText, !sentOrEdited, edit, parseMode,
                    replyMarkup);
                currentText = "";
                sentOrEdited = true;
            }

            currentText += splittedText[i] + splitValue;
        }

        return await SendOrEditMessage(messageId, chatId, botClient, currentText, !sentOrEdited, edit, parseMode,
            replyMarkup);
    }
}