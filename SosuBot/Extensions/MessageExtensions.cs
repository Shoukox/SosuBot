using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;
using OsuApi.Core.V2.Scores.Models;

namespace SosuBot.Extensions
{
    public static class MessageExtensions
    {
        public static string? GetCommand(this string text)
        {
            if (text.Length == 0 || text[0] != '/') return null;

            text = text.Trim();
            int spaceIndex = text.IndexOf(' ');
            if (spaceIndex == -1) return text;
            return text[0..spaceIndex];
        }

        public static string[]? GetCommandParameters(this string text)
        {
            if (text.Length == 0 || text[0] != '/') return null;
            return text.Split(' ')[1..];
        }

        /// <summary>
        /// Tries to convert the user's input into a <see cref="Ruleset"/> string
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static string? ParseToRuleset(this string text)
        {
            text = text.Trim().ToLowerInvariant();

            // user can type taiko/mania, but fruits can be written in another way
            string[] possibilitiesOfFruitsInput = ["ctb", "catch"];

            // osu ruleset can be also written in some another way
            string[] possibilitiesOfOsuInput = ["osu", "std", "standard", "standart"];

            if (possibilitiesOfFruitsInput.Contains(text)) text = Ruleset.Fruits;
            else if (possibilitiesOfOsuInput.Contains(text)) text = Ruleset.Osu;
            else if (text is not Ruleset.Taiko and not Ruleset.Mania) return null;

            return text;
        }

        /// <summary>
        /// Tries to convert a <see cref="Ruleset"/> string into a more readable and user friendly version
        /// </summary>
        /// <param name="ruleset"></param>
        /// <returns></returns>
        public static string? ParseFromRuleset(this string ruleset)
        {
            return ruleset switch
            {
                Ruleset.Osu => "osu!std",
                Ruleset.Mania => "osu!mania",
                Ruleset.Taiko => "osu!taiko",
                Ruleset.Fruits => "osu!catch",
                _ => null
            };
        }

        public static Task<Message> ReplyAsync(this Message message, ITelegramBotClient botClient, string text, bool privateAsnwer = false, ParseMode parseMode = ParseMode.Html)
          => botClient.SendMessage(privateAsnwer ? message.From!.Id : message.Chat.Id, text, parseMode: parseMode, replyParameters: new ReplyParameters() { MessageId = message.MessageId }, linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true });

        public static Task<Message> EditAsync(this Message message, ITelegramBotClient botClient, string text, ParseMode parseMode = ParseMode.Html, InlineKeyboardMarkup? replyMarkup = null)
          => botClient.EditMessageText(message.Chat.Id, message.MessageId, text, parseMode: parseMode, linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true }, replyMarkup: replyMarkup);
    }
}
