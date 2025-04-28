using OppaiSharp;
using Sosu.osu.V1.Types;
using Sosu.Services.ProcessUpdate.Tools;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Beatmap = Sosu.osu.V1.Types.Beatmap;
using Sosu.Localization;

namespace Sosu.Services.ProcessUpdate.MessageCommands
{
    public class OsuScoreCommand : ICommand
    {
        public Func<ITelegramBotClient, Update, Task> action => new Func<ITelegramBotClient, Update, Task>(async (bot, update) =>
        {
            var message = update.Message;
            var user = Variables.osuUsers.FirstOrDefault(m => m.telegramId == message.From.Id);
            Score[]? scores = default;
            long beatmap_id = default;
            var chat = Variables.chats.First(m => m.chat.Id == message.Chat.Id);
            string osunickname = "";
            ILocalization language = Localization.Localization.Methods.GetLang(chat.language);

            Message startMessage = await bot.SendTextMessageAsync(message.Chat.Id, language.waiting, replyToMessageId: message.MessageId, parseMode: ParseMode.Html);
            string[] splittedMessage = message.Text.Split(" ");

            if (user == default)
            {
                await bot.EditMessageTextAsync(message.Chat.Id, startMessage.MessageId, language.error_noUser, ParseMode.Html);
                return;
            }

            if (splittedMessage.Length == 1)
            {
                ParsedBeatmap? parsedBeatmap = null;
                Beatmap? beatmap = null;
                parsedBeatmap = (message.ReplyToMessage != null && message.ReplyToMessage.Text != null) ? BeatmapLinkParser.Parse(message.ReplyToMessage.Text) : new ParsedBeatmap(chat.lastBeatmap_id, false, Mods.NoMod);
                if (parsedBeatmap == null)
                {
                    var parsedUrlInEntities = message.ReplyToMessage.Entities.Where(m => m.Type == MessageEntityType.TextLink).Select(m => BeatmapLinkParser.Parse(m.Url)).FirstOrDefault(m => m != null);
                    if (parsedUrlInEntities == default)
                    {
                        await bot.EditMessageTextAsync(message.Chat.Id, startMessage.MessageId, language.error_noRecords, ParseMode.Html);
                        return;
                    }
                    parsedBeatmap = parsedUrlInEntities;

                }
                beatmap = await parsedBeatmap.Parse();
                beatmap_id = beatmap == null ? Variables.chats.First(m => m.chat.Id == message.Chat.Id).lastBeatmap_id : int.Parse(beatmap.beatmap_id);
                scores = await Variables.osuApi.GetScoresOnMapByName(user.osuName, beatmap_id);
                osunickname = user.osuName;
            }
            else if (splittedMessage.Length == 2)
            {
                ParsedBeatmap? parsedBeatmap = null;
                Beatmap? beatmap = null;

                parsedBeatmap = BeatmapLinkParser.Parse(splittedMessage[1]);
                if (parsedBeatmap != null)
                {
                    beatmap = await parsedBeatmap.Parse();
                    beatmap_id = int.Parse(beatmap.beatmap_id);
                    scores = await Variables.osuApi.GetScoresOnMapByName(user.osuName, beatmap_id);
                    osunickname = user.osuName;
                }
                else if (message.ReplyToMessage != null)
                {
                    parsedBeatmap = BeatmapLinkParser.Parse(message.ReplyToMessage.Text);
                    beatmap = await parsedBeatmap.Parse();
                    beatmap_id = int.Parse(beatmap.beatmap_id);
                    scores = await Variables.osuApi.GetScoresOnMapByName(splittedMessage[1], beatmap_id);
                    osunickname = splittedMessage[1];
                }
            }
            if (scores == default)
            {
                await bot.EditMessageTextAsync(message.Chat.Id, startMessage.MessageId, language.error_noRecords, ParseMode.Html);
                return;
            }
            string textToSend = $"<b>{osunickname}</b>\n\n";
            for (int i = 0; i <= scores.Length - 1; i++)
            {
                var score = scores[i];
                Mods mods = (Mods)Variables.osuApi.CalculateModsMods(int.Parse(score.enabled_mods));

                double accuracy = (50 * double.Parse(score.count50) + 100 * double.Parse(score.count100) + 300 * double.Parse(score.count300)) / (300 * (double.Parse(score.countmiss) + double.Parse(score.count50) + double.Parse(score.count100) + double.Parse(score.count300))) * 100;
                Beatmap beatmap = await Variables.osuApi.GetBeatmapByBeatmapIdAsync(beatmap_id);
                if (i == 0)
                {
                    chat.lastBeatmap_id = beatmap_id;
                }

                double curpp = -1;
                if (score.pp != null)
                    curpp = double.Parse(score.pp);
                else
                    curpp = (await Sosu.Services.ProcessUpdate.Tools.PPCalc.ppCalc(beatmap_id, accuracy, (OppaiSharp.Mods)mods, int.Parse(score.countmiss), int.Parse(score.maxcombo)))[0];
                beatmap.ParseHTML();
                textToSend += Localization.Localization.Methods.ReplaceEmpty(language.command_score, new[] { $"{score.rank}", $"{beatmap_id}", $"{beatmap.title}", $"{beatmap.version}", $"{beatmap.GetApproved()}", $"{score.count300}", $"{score.count100}", $"{score.count50}", $"{score.countmiss}", $"{score.accuracy():N2}", $"{mods}", $"{score.maxcombo}", $"{beatmap.max_combo}", $"{curpp:N2}", $"{DateTimeOffset.Parse(score.date).AddHours(5):dd.MM.yyyy HH:mm zzz}" });
            }
            //Variables.db.InsertOrUpdateOsuChatsTable(chat, false);
            await bot.EditMessageTextAsync(startMessage.Chat.Id, startMessage.MessageId, textToSend, ParseMode.Html, disableWebPagePreview: true);
        });
    }
}
