using OppaiSharp;
using Sosu.osu.V1.Types;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Sosu.Localization;

namespace Sosu.Services.ProcessUpdate.CallbacksCommands
{
    public class OsuUserBestCallbackCommand : ICommand
    {
        public Func<ITelegramBotClient, Update, Task> action => new Func<ITelegramBotClient, Update, Task>(async (bot, update) =>
        {
            var callback = update.CallbackQuery;
            var chat = Variables.chats.FirstOrDefault(m => m.chat.Id == callback.Message.Chat.Id);
            ILocalization language = Localization.Localization.Methods.GetLang(chat.language);

            string[] splittedCallback = callback.Data.Split(' ');

            if (splittedCallback[2] == "previous" && splittedCallback[3] == "0")
            {
                await bot.AnswerCallbackQueryAsync(callback.Id, language.error_noPreviousScores, true);
                return;
            }

            Score[] scores = null;
            int step = int.Parse(splittedCallback[3]);
            string action = splittedCallback[2];
            int gameMode = int.Parse(splittedCallback[4]);
            string mode = Variables.osuApi.GetGameMode(gameMode);
            string name = "";
            for (int i = 5; i <= splittedCallback.Length - 1; i++)
                name += splittedCallback[i];

            if (action == "next")
            {
                scores = await Variables.osuApi.GetUserBestByNameAsync(name, 5 * (step + 2), gameMode);
                int takecount = scores.Length >= 5 ? 5 : scores.Length;
                scores = scores.TakeLast(takecount).ToArray();
                step += 1;
            }
            else if (action == "previous")
            {
                scores = await Variables.osuApi.GetUserBestByNameAsync(name, 5 * step, gameMode);
                int takecount = scores.Length >= 5 ? 5 : scores.Length;
                scores = scores.TakeLast(takecount).ToArray();
                step -= 1;
            }

            string textToSend = $"{name}({mode})\n\n";
            int index = step * 5;
            foreach (var item in scores)
            {
                Mods mods = (Mods)Variables.osuApi.CalculateModsMods(int.Parse(item.enabled_mods));
                double accuracy = (50 * double.Parse(item.count50) + 100 * double.Parse(item.count100) + 300 * double.Parse(item.count300)) / (300 * (double.Parse(item.countmiss) + double.Parse(item.count50) + double.Parse(item.count100) + double.Parse(item.count300))) * 100;
                Sosu.osu.V1.Types.Beatmap beatmap = await Variables.osuApi.GetBeatmapByBeatmapIdAsync(long.Parse(item.beatmap_id));
                beatmap.ParseHTML();
                textToSend += Localization.Localization.Methods.ReplaceEmpty(language.command_userbest, new[] { $"{index + 1}", $"{item.rank}", $"{beatmap.beatmap_id}", $"{beatmap.title}", $"{beatmap.version}", $"{beatmap.GetApproved()}", $"{item.count300}", $"{item.count100}", $"{item.count50}", $"{item.countmiss}", $"{item.accuracy():N2}", $"{mods}", $"{item.maxcombo}", $"{beatmap.max_combo}", $"{double.Parse(item.pp)}" });
                index += 1;
            }
            var ik = new InlineKeyboardMarkup(
               new InlineKeyboardButton[] { new InlineKeyboardButton("Previous") { CallbackData = $"{callback.Message.Chat.Id} userbest previous {step} {splittedCallback[4]} {name}" }, new InlineKeyboardButton("Next") { CallbackData = $"{callback.Message.Chat.Id} userbest next {step} {splittedCallback[4]} {name}" } }
               );
            await bot.EditMessageTextAsync(callback.Message.Chat.Id, callback.Message.MessageId, textToSend, Telegram.Bot.Types.Enums.ParseMode.Html, replyMarkup: ik, disableWebPagePreview: true);
            await bot.AnswerCallbackQueryAsync(callback.Id);

        });
    }
}
