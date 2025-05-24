using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace SosuBot.Extensions
{
    public static class TelegramUpdateExtensions
    {
        public static Task<Message> ReplyAsync(this Message message, ITelegramBotClient botClient, string text, bool privateAsnwer = false, ParseMode parseMode = ParseMode.Html, InlineKeyboardMarkup? replyMarkup = null)
          => botClient.SendMessage(privateAsnwer ? message.From!.Id : message.Chat.Id, text, parseMode: parseMode, replyParameters: new ReplyParameters() { MessageId = message.MessageId }, linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true }, replyMarkup: replyMarkup);

        public static Task<Message> ReplyPhotoAsync(this Message message, ITelegramBotClient botClient, InputFile photo, string? caption = null, bool privateAsnwer = false, ParseMode parseMode = ParseMode.Html, InlineKeyboardMarkup? replyMarkup = null)
          => botClient.SendPhoto(privateAsnwer ? message.From!.Id : message.Chat.Id, photo, caption, parseMode: parseMode, replyParameters: new ReplyParameters() { MessageId = message.MessageId }, replyMarkup: replyMarkup);

        public static Task<Message> ReplyDocumentAsync(this Message message, ITelegramBotClient botClient, InputFile document, string? caption = null, bool privateAsnwer = false, ParseMode parseMode = ParseMode.Html, InlineKeyboardMarkup? replyMarkup = null)
          => botClient.SendDocument(privateAsnwer ? message.From!.Id : message.Chat.Id, document, caption, parseMode: parseMode, replyParameters: new ReplyParameters() { MessageId = message.MessageId }, replyMarkup: replyMarkup);

        public static Task<Message> EditAsync(this Message message, ITelegramBotClient botClient, string text, ParseMode parseMode = ParseMode.Html, InlineKeyboardMarkup? replyMarkup = null)
          => botClient.EditMessageText(message.Chat.Id, message.MessageId, text, parseMode: parseMode, linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true }, replyMarkup: replyMarkup);

        public static Task AnswerAsync(this CallbackQuery callbackQuery, ITelegramBotClient botClient, string? text = null, bool showAlert = false)
          => botClient.AnswerCallbackQuery(callbackQuery.Id, text, showAlert);
    }
}
