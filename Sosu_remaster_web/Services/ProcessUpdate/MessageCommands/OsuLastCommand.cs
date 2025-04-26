using OppaiSharp;
using Sosu.osu.V1.Types;
using Sosu.Services.ProcessUpdate.Tools;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Sosu.Localization;
using TagLib.Ape;
using osu.Game.Rulesets.Mods;
using Sosu.osu.V1.Enums;
using osu.Game.Rulesets.Osu.Mods;

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
                osu.V1.Enums.Mods mods = (osu.V1.Enums.Mods)Variables.osuApi.CalculateModsMods(int.Parse(score.enabled_mods));

                osu.V1.Types.Beatmap beatmap = await Variables.osuApi.GetBeatmapByBeatmapIdAsync(int.Parse(score.beatmap_id));
                if (i == 0) chat.lastBeatmap_id = long.Parse(beatmap.beatmap_id);
                beatmap.ParseHTML();

                //double[] curpp = new[]
                //{Other.ppCalc1(long.Parse(beatmap.beatmap_id), item.accuracy(), (OppaiSharp.Mods)mods, int.Parse(item.count100), int.Parse(item.count50), int.Parse(item.countmiss), int.Parse(item.maxcombo)),
                //Other.ppCalc1(long.Parse(beatmap.beatmap_id), item.accuracy(), (OppaiSharp.Mods)mods, int.Parse(item.count100), int.Parse(item.count50), 0, int.Parse(beatmap.max_combo))};
                //double[] curpp = await Tools.PPCalc.ppCalc(long.Parse(beatmap.beatmap_id), score.accuracy(), (OppaiSharp.Mods)mods, int.Parse(score.countmiss), int.Parse(score.maxcombo));
                
                Mod[] getMods(osu.V1.Enums.Mods mods)
                {
                    List<Mod> osuMods = new List<Mod>();
                    if (mods.HasFlag(osu.V1.Enums.Mods.Perfect)) osuMods.Add(new OsuModPerfect());
                    if (mods.HasFlag(osu.V1.Enums.Mods.Hardrock)) osuMods.Add(new OsuModHardRock());
                    if (mods.HasFlag(osu.V1.Enums.Mods.DoubleTime)) osuMods.Add(new OsuModDoubleTime());
                    if (mods.HasFlag(osu.V1.Enums.Mods.Easy)) osuMods.Add(new OsuModEasy());
                    if (mods.HasFlag(osu.V1.Enums.Mods.Flashlight)) osuMods.Add(new OsuModFlashlight());
                    if (mods.HasFlag(osu.V1.Enums.Mods.HalfTime)) osuMods.Add(new OsuModHalfTime());
                    if (mods.HasFlag(osu.V1.Enums.Mods.Hidden)) osuMods.Add(new OsuModHidden());
                    if (mods.HasFlag(osu.V1.Enums.Mods.Nightcore)) osuMods.Add(new OsuModNightcore());
                    if (mods.HasFlag(osu.V1.Enums.Mods.NoFail)) osuMods.Add(new OsuModNoFail());
                    if (mods.HasFlag(osu.V1.Enums.Mods.SpunOut)) osuMods.Add(new OsuModSpunOut());
                    if (mods.HasFlag(osu.V1.Enums.Mods.SuddenDeath)) osuMods.Add(new OsuModSuddenDeath());
                    if (mods.HasFlag(osu.V1.Enums.Mods.TouchDevice)) osuMods.Add(new OsuModTouchDevice());

                    osuMods.Add(new OsuModClassic());
                    return osuMods.ToArray();
                }
                ;
                var curpp = new
                {
                    Current = await new PerfomanceCalculator.PPCalculator().CalculatePPAsync(int.Parse(beatmap.beatmap_id), int.Parse(score.count300), int.Parse(score.count100), int.Parse(score.count50), int.Parse(score.countmiss), int.Parse(score.maxcombo),
                              getMods(mods)),
                    NoMiss = await new PerfomanceCalculator.PPCalculator().CalculatePPAsync(int.Parse(beatmap.beatmap_id), int.Parse(score.count300), int.Parse(score.count100), int.Parse(score.count50), 0, int.Parse(beatmap.max_combo),
                              getMods(mods)),
                };
                textToSend += Localization.Localization.Methods.ReplaceEmpty(language.command_last, new[] { $"{i + 1}", $"{score.rank}", $"{score.beatmap_id}", $"{beatmap.title}", $"{beatmap.version}", $"{beatmap.GetApproved()}", $"{score.count300}", $"{score.count100}", $"{score.count50}", $"{score.countmiss}", $"{score.accuracy():N2}", $"{mods}", $"{score.maxcombo}", $"{beatmap.max_combo}", $"{curpp.Current}", $"{curpp.NoMiss}", $"{DateTimeOffset.Parse(score.date).AddHours(5):dd.MM.yyyy HH:mm} UTC+05:00", $"{score.completion(beatmap.countobjects()):N1}" });
            }
            //Variables.db.InsertOrUpdateOsuChatsTable(chat, false);
            await bot.EditMessageTextAsync(startMessage.Chat.Id, startMessage.MessageId, textToSend, ParseMode.Html, disableWebPagePreview: true);

        });


    }
}
