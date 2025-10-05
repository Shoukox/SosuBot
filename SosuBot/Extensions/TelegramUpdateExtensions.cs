using SosuBot.Helpers.OutputText;
using SosuBot.Synchronization.MessageSpamResistance;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace SosuBot.Extensions;

public static class TelegramUpdateExtensions
{
    public static Task<Message> ReplyAsync(this Message message, ITelegramBotClient botClient, string text,
        bool privateAsnwer = false, ParseMode parseMode = ParseMode.Html,
        InlineKeyboardMarkup? replyMarkup = null, string? splitValue = null)
    {
        if (splitValue == null)
            return TelegramHelper.SendMessageConsideringTelegramLength(message.Id, message.Chat.Id, botClient, text,
                parseMode, replyMarkup);

        return TelegramHelper.SendMessageConsideringTelegramLengthAndSplitValue(message.Id, message.Chat.Id, botClient,
            text,
            parseMode, replyMarkup, false, splitValue);
    }

    public static Task<Message> ReplyPhotoAsync(this Message message, ITelegramBotClient botClient, InputFile photo,
        string? caption = null, bool privateAsnwer = false, ParseMode parseMode = ParseMode.Html,
        InlineKeyboardMarkup? replyMarkup = null)
    {
        return botClient.SendPhoto(privateAsnwer ? message.From!.Id : message.Chat.Id, photo, caption,
            parseMode, new ReplyParameters { MessageId = message.MessageId },
            replyMarkup);
    }

    public static Task<Message> ReplyDocumentAsync(this Message message, ITelegramBotClient botClient,
        InputFile document, string? caption = null, bool privateAsnwer = false,
        ParseMode parseMode = ParseMode.Html, InlineKeyboardMarkup? replyMarkup = null)
    {
        return botClient.SendDocument(privateAsnwer ? message.From!.Id : message.Chat.Id, document, caption,
            parseMode, new ReplyParameters { MessageId = message.MessageId },
            replyMarkup);
    }

    public static Task<Message> EditAsync(this Message message, ITelegramBotClient botClient, string text,
        ParseMode parseMode = ParseMode.Html, InlineKeyboardMarkup? replyMarkup = null, string? splitValue = null)
    {
        if (splitValue == null)
            return TelegramHelper.SendMessageConsideringTelegramLength(message.Id, message.Chat.Id, botClient, text,
                parseMode, replyMarkup, true);

        return TelegramHelper.SendMessageConsideringTelegramLengthAndSplitValue(message.Id, message.Chat.Id, botClient,
            text,
            parseMode, replyMarkup, true, splitValue);
    }

    public static Task AnswerAsync(this CallbackQuery callbackQuery, ITelegramBotClient botClient,
        string? text = null, bool showAlert = false)
    {
        return botClient.AnswerCallbackQuery(callbackQuery.Id, text, showAlert);
    }

    public static IEnumerable<string> GetAllLinks(this Message message)
    {
        if (message.Text == null || message.Entities == null) return [];

        List<string> links = [];
        foreach (var me in message.Entities.Where(e =>
                     e.Type is MessageEntityType.Url or MessageEntityType.TextLink))
            links.Add(me.Url ?? message.Text.Substring(me.Offset, me.Length));

        return links;
    }

    /// <summary>
    /// </summary>
    /// <param name="msg"></param>
    /// <param name="botClient"></param>
    /// <returns>True, if is banned</returns>
    public static async Task<bool> IsUserSpamming(this Message msg, ITelegramBotClient botClient)
    {
        var (canSend, sendWarning) = await SpamResistance.Instance.CanSendMessage(msg.From!.Id, msg.Date);
        if (!canSend)
        {
            if (sendWarning)
            {
                await Task.Delay(1000);
                await botClient.SendMessage(msg.Chat.Id,
                    $"Не спамь!\nТы заблокирован на {SpamResistance.BlockInterval.TotalSeconds} сек.",
                    ParseMode.Html, msg.MessageId, linkPreviewOptions: false);
            }

            return true;
        }

        return false;
    }
}