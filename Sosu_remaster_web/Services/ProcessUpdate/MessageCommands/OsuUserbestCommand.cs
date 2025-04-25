using OppaiSharp;
using Sosu.osu.V1.Types;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Beatmap = Sosu.osu.V1.Types.Beatmap;
using Sosu.Localization;

namespace Sosu.Services.ProcessUpdate.MessageCommands
{
    public class OsuUserbestCommand : ICommand
    {
        public Func<ITelegramBotClient, Update, Task> action => new Func<ITelegramBotClient, Update, Task>(async (bot, update) =>
        {
            var message = update.Message;
            var user = Variables.osuUsers.FirstOrDefault(m => m.telegramId == message.From.Id);
            var chat = Variables.chats.FirstOrDefault(m => m.chat.Id == message.Chat.Id);
            ILocalization language = Localization.Localization.Methods.GetLang(chat.language);

            Message startMessage = await bot.SendTextMessageAsync(message.Chat.Id, language.waiting, replyToMessageId: message.MessageId);
            string[] splittedMessage = message.Text.Split(' ');
            Score[]? scores = null;
            int gameMode = 0;
            Sosu.osu.V1.Types.User? osuUser = null;
            if (splittedMessage.Length == 1)
            {
                if (user == default)
                {
                    await bot.EditMessageTextAsync(message.Chat.Id, startMessage.MessageId, language.error_noUser, ParseMode.Html);
                    return;
                }

                scores = await Variables.osuApi.GetUserBestByNameAsync(user.osuName, 5);
                osuUser = await Variables.osuApi.GetUserInfoByNameAsync(user.osuName, gameMode);
            }
            else if (splittedMessage.Length == 2)
            {
                scores = await Variables.osuApi.GetUserBestByNameAsync(splittedMessage[1], 5);
                osuUser = await Variables.osuApi.GetUserInfoByNameAsync(splittedMessage[1], gameMode);
            }
            else if (splittedMessage.Length == 3)
            {
                gameMode = int.Parse(splittedMessage[2]);
                scores = await Variables.osuApi.GetUserBestByNameAsync(splittedMessage[1], 5, gameMode);
                osuUser = await Variables.osuApi.GetUserInfoByNameAsync(splittedMessage[1], gameMode);
            }

            if (scores == null)
            {
                await bot.EditMessageTextAsync(message.Chat.Id, startMessage.MessageId, language.error_noRecords, ParseMode.Html);
                return;
            }
            string mode = Variables.osuApi.GetGameMode(gameMode);
            string textToSend = $"{osuUser.username()}({mode})\n\n";

            int i = 0;
            foreach (var item in scores)
            {
                Mods mods = (Mods)Variables.osuApi.CalculateModsMods(int.Parse(item.enabled_mods));
                Beatmap beatmap = await Variables.osuApi.GetBeatmapByBeatmapIdAsync(long.Parse(item.beatmap_id));
                beatmap.ParseHTML();
                textToSend += Localization.Localization.Methods.ReplaceEmpty(language.command_userbest, new[] { $"{i + 1}", $"{item.rank}", $"{beatmap.beatmap_id}", $"{beatmap.title}", $"{beatmap.version}", $"{beatmap.GetApproved()}", $"{item.count300}", $"{item.count100}", $"{item.count50}", $"{item.countmiss}", $"{item.accuracy():N2}", $"{mods}", $"{item.maxcombo}", $"{beatmap.max_combo}", $"{double.Parse(item.pp)}" });
                i += 1;
            }
            var ik = new InlineKeyboardMarkup(
                new InlineKeyboardButton[] { new InlineKeyboardButton("Previous") { CallbackData = $"{chat.chat.Id} userbest previous 0 {gameMode} {osuUser.username()}" }, new InlineKeyboardButton("Next") { CallbackData = $"{chat.chat.Id} userbest next 0 {gameMode} {osuUser.username()}" } }
                );
            await bot.EditMessageTextAsync(message.Chat.Id, startMessage.MessageId, textToSend, ParseMode.Html, replyMarkup: ik, disableWebPagePreview: true);

        });
    }
}
