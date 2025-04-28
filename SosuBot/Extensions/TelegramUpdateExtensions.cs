using OsuApi.Core.V2.Scores.Models;
using SosuBot.OsuTypes;
using System.Text.RegularExpressions;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace SosuBot.Extensions
{
    public static class TelegramUpdateExtensions
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
            text = text.Trim().ToLowerInvariant().Replace("mode=", "");

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
        public static string ParseRulesetToGamemode(this string ruleset)
        {
            return ruleset switch
            {
                Ruleset.Osu => "osu!std",
                Ruleset.Mania => "osu!mania",
                Ruleset.Taiko => "osu!taiko",
                Ruleset.Fruits => "osu!catch",
                _ => throw new NotImplementedException()    
            };
        }

        public static Playmode ParseRulesetToPlaymode(this string ruleset)
        {
            return ruleset switch
            {
                Ruleset.Osu => Playmode.Osu,
                Ruleset.Taiko => Playmode.Taiko,
                Ruleset.Fruits => Playmode.Catch,
                Ruleset.Mania => Playmode.Mania,
                _ => throw new NotImplementedException()
            };
        }

        public static Task<Message> ReplyAsync(this Message message, ITelegramBotClient botClient, string text, bool privateAsnwer = false, ParseMode parseMode = ParseMode.Html, InlineKeyboardMarkup? replyMarkup = null)
          => botClient.SendMessage(privateAsnwer ? message.From!.Id : message.Chat.Id, text, parseMode: parseMode, replyParameters: new ReplyParameters() { MessageId = message.MessageId }, linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true }, replyMarkup: replyMarkup);

        public static Task<Message> EditAsync(this Message message, ITelegramBotClient botClient, string text, ParseMode parseMode = ParseMode.Html, InlineKeyboardMarkup? replyMarkup = null)
          => botClient.EditMessageText(message.Chat.Id, message.MessageId, text, parseMode: parseMode, linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true }, replyMarkup: replyMarkup);

        public static Task AnswerAsync(this CallbackQuery callbackQuery, ITelegramBotClient botClient)
          => botClient.AnswerCallbackQuery(callbackQuery.Id);
    }
}
