using OsuApi.Core.V2.Scores.Models;
using SosuBot.OsuTypes;
using System.Text.Encodings.Web;
using System.Web;
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

        public static Task<Message> ReplyAsync(this Message message, ITelegramBotClient botClient, InputFile photo, string caption, bool privateAsnwer = false, ParseMode parseMode = ParseMode.Html, InlineKeyboardMarkup? replyMarkup = null)
          => botClient.SendPhoto(privateAsnwer ? message.From!.Id : message.Chat.Id, photo, caption, parseMode: parseMode, replyParameters: new ReplyParameters() { MessageId = message.MessageId }, replyMarkup: replyMarkup);

        public static Task<Message> EditAsync(this Message message, ITelegramBotClient botClient, string text, ParseMode parseMode = ParseMode.Html, InlineKeyboardMarkup? replyMarkup = null)
          => botClient.EditMessageText(message.Chat.Id, message.MessageId, text, parseMode: parseMode, linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true }, replyMarkup: replyMarkup);

        public static Task AnswerAsync(this CallbackQuery callbackQuery, ITelegramBotClient botClient)
          => botClient.AnswerCallbackQuery(callbackQuery.Id);
    }
}
