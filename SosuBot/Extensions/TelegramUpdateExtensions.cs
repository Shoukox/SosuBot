﻿using Polly;
using SDL;
using SosuBot.Synchonization.MessageSpamResistance;
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

        public static IEnumerable<string> GetAllLinks(this Message message)
        {
            if(message.Text == null || message.Entities == null) return Enumerable.Empty<string>();

            List<string> links = new List<string>();
            foreach(MessageEntity me in message.Entities.Where(e => e.Type is MessageEntityType.Url or MessageEntityType.TextLink))
            {
                links.Add(me.Url ?? message.Text.Substring(me.Offset, me.Length));
            }
            return links;
        }

        /// <summary>
        /// 
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
                    await botClient.SendMessage(msg.Chat.Id, $"Не спамь!\nТы заблокирован на {SpamResistance.BlockInterval.TotalSeconds} сек.", ParseMode.Html, msg.MessageId, linkPreviewOptions: false);
                }
                return true;
            }
            return false;
        }
    }
}
