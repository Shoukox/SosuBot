using OppaiSharp;
using Sosu.osu.V1.Types;
using Sosu.Services.ProcessUpdate.Tools;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Sosu.Localization;

namespace Sosu.Services.ProcessUpdate.MessageCommands
{
    public class OsuLastCommand : ICommand
    {
        public Func<ITelegramBotClient, Update, Task> action => new Func<ITelegramBotClient, Update, Task>(async (bot, update) =>
        {
            var message = update.Message;
            var user = Variables.osuUsers.FirstOrDefault(m => m.telegramId == message.From.Id);
            var chat = Variables.chats.FirstOrDefault(m => m.chat.Id == message.Chat.Id);
            string osunickname = "";
            string[] splittedMessage = message.Text.Split(" ");

            ILocalization language = Localization.Localization.Methods.GetLang(chat.language);

            Score[]? scores = null;

            Message startMessage = await bot.SendTextMessageAsync(message.Chat.Id, language.waiting, replyToMessageId: message.MessageId, parseMode: ParseMode.Html);




            //await Variables.osuApiV2.GetRecentScoresByNameAsync(user.osuName, 1);

            if (splittedMessage.Length == 3)
            {
                scores = await Variables.osuApi.GetRecentScoresByNameAsync(splittedMessage[1], (splittedMessage.Length == 2) ? 1 : int.Parse(splittedMessage[2]));
                osunickname = splittedMessage[1];
            }
            if (splittedMessage.Length == 2)
            {
                if (splittedMessage[1].Length == 1)
                {
                    if (user == default)
                    {
                        await bot.EditMessageTextAsync(startMessage.Chat.Id, startMessage.MessageId, language.error_noUser, ParseMode.Html);
                        return;
                    }
                    else
                    {
                        scores = await Variables.osuApi.GetRecentScoresByNameAsync(user.osuName, int.Parse(splittedMessage[1]));
                        osunickname = user.osuName;
                    }
                }
                else
                {
                    scores = await Variables.osuApi.GetRecentScoresByNameAsync(splittedMessage[1], 1);
                    osunickname = splittedMessage[1];
                }
            }
            if (splittedMessage.Length == 1)
            {
                if (user == default)
                {
                    await bot.EditMessageTextAsync(startMessage.Chat.Id, startMessage.MessageId, language.error_noUser, ParseMode.Html);
                    return;
                }
                else
                {
                    scores = await Variables.osuApi.GetRecentScoresByNameAsync(user.osuName, 1);
                    osunickname = user.osuName;
                }
            }
            if (scores == default)
            {
                await bot.EditMessageTextAsync(startMessage.Chat.Id, startMessage.MessageId, language.error_noRecords, ParseMode.Html);
                return;
            }
            string textToSend = $"<b>{osunickname}</b>\n\n";
            for (int i = 0; i <= scores.Length - 1; i++)
            {
                var score = scores[i];
                Mods mods = (Mods)Variables.osuApi.CalculateModsMods(int.Parse(score.enabled_mods));

                osu.V1.Types.Beatmap beatmap = await Variables.osuApi.GetBeatmapByBeatmapIdAsync(int.Parse(score.beatmap_id));
                if (i == 0) chat.lastBeatmap_id = long.Parse(beatmap.beatmap_id);
                beatmap.ParseHTML();

                //double[] curpp = new[]
                //{Other.ppCalc1(long.Parse(beatmap.beatmap_id), item.accuracy(), (OppaiSharp.Mods)mods, int.Parse(item.count100), int.Parse(item.count50), int.Parse(item.countmiss), int.Parse(item.maxcombo)),
                //Other.ppCalc1(long.Parse(beatmap.beatmap_id), item.accuracy(), (OppaiSharp.Mods)mods, int.Parse(item.count100), int.Parse(item.count50), 0, int.Parse(beatmap.max_combo))};
                double[] curpp = await Tools.PPCalc.ppCalc(long.Parse(beatmap.beatmap_id), score.accuracy(), (OppaiSharp.Mods)mods, int.Parse(score.countmiss), int.Parse(score.maxcombo));
                textToSend += Localization.Localization.Methods.ReplaceEmpty(language.command_last, new[] { $"{i + 1}", $"{score.rank}", $"{score.beatmap_id}", $"{beatmap.title}", $"{beatmap.version}", $"{beatmap.GetApproved()}", $"{score.count300}", $"{score.count100}", $"{score.count50}", $"{score.countmiss}", $"{score.accuracy():N2}", $"{mods}", $"{score.maxcombo}", $"{beatmap.max_combo}", $"{curpp[0]}", $"{curpp[1]}", $"{DateTimeOffset.Parse(score.date).AddHours(5):dd.MM.yyyy HH:mm} UTC+05:00", $"{score.completion(beatmap.countobjects()):N1}" });
            }
            //Variables.db.InsertOrUpdateOsuChatsTable(chat, false);
            await bot.EditMessageTextAsync(startMessage.Chat.Id, startMessage.MessageId, textToSend, ParseMode.Html, disableWebPagePreview: true);

        });
    }
}
