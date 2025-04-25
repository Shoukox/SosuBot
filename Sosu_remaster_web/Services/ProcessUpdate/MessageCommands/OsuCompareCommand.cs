using Sosu.Localization;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Sosu.Services.ProcessUpdate.MessageCommands
{
    public class OsuCompareCommand : ICommand
    {
        public Func<ITelegramBotClient, Update, Task> action => new Func<ITelegramBotClient, Update, Task>(async (bot, update) =>
        {
            var message = update.Message;
            var user = Variables.osuUsers.FirstOrDefault(m => m.telegramId == message.From.Id);
            var chat = Variables.chats.FirstOrDefault(m => m.chat.Id == message.Chat.Id);
            ILocalization language = Localization.Localization.Methods.GetLang(chat.language);

            Message startMessage = await bot.SendTextMessageAsync(message.Chat.Id, language.waiting, replyToMessageId: message.MessageId);
            string[] splittedMessage = message.Text.Split(' ');

            if (splittedMessage.Length < 3)
            {
                await bot.EditMessageTextAsync(message.Chat.Id, startMessage.MessageId, language.error_argsLength, ParseMode.Html);
                return;
            }

            Sosu.osu.V1.Types.User? user1 = null;
            Sosu.osu.V1.Types.User? user2 = null;
            int gamemode = splittedMessage.Length == 3 ? 0 : int.Parse(splittedMessage[3]);
            user1 = await Variables.osuApi.GetUserInfoByNameAsync(splittedMessage[1], gamemode);
            user2 = await Variables.osuApi.GetUserInfoByNameAsync(splittedMessage[2], gamemode);

            if (user1 == null || user2 == null)
            {
                await bot.EditMessageTextAsync(message.Chat.Id, startMessage.MessageId, language.error_userNotFound, ParseMode.Html);
                return;
            }

            string acc1 = $"{double.Parse(user1.accuracy()):N2}";
            string acc2 = $"{double.Parse(user2.accuracy()):N2}";
            int max = new[] { user1.pp_country_rank().Length + "# UZ".Length, user1.pp_rank().Length + "#".Length, user1.pp_raw().ToString().Length + "pp".Length, acc1.Length + "%".Length, $"{user1.playtime_hours()}h".Length, user1.username().Length }.Max();

            string textToSend = Localization.Localization.Methods.ReplaceEmpty(language.command_compare, new[] { $"{Variables.osuApi.GetGameMode(gamemode)}", $"{user1.username().PadRight(max)}", $"{user2.username()}", $"{("#" + user1.pp_rank()).PadRight(max)}", $"{user2.pp_rank()}", $"{("#" + user1.pp_country_rank() + " " + user1.country()).PadRight(max)}", $"{(user2.pp_country_rank() + " " + user2.country())}", $"{(user1.pp_raw() + "pp").PadRight(max)}", $"{user2.pp_raw()}", $"{(acc1 + "%").PadRight(max)}", $"{acc2}%", $"{(user1.playtime_hours().ToString() + "h").PadRight(max)}", $"{user2.playtime_hours()}" });
            await bot.EditMessageTextAsync(message.Chat.Id, startMessage.MessageId, textToSend, ParseMode.Html, disableWebPagePreview: true);

        });
    }
}
