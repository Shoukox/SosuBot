using Sosu.osu.V1.Types;
using Sosu.Services.ProcessUpdate.Tools;
using System.Text.RegularExpressions;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InputFiles;
using Telegram.Bot.Types.ReplyMarkups;
using Beatmap = Sosu.osu.V1.Types.Beatmap;
using Sosu.Localization;

namespace Sosu.Services.ProcessUpdate.MessageCommands
{
    public class OtherCommand : ICommand
    {
        private static readonly Regex linkRegex = new(@"(?>https?:\/\/)?(?>osu|old)\.ppy\.sh\/([b,s]|(?>beatmaps)|(?>beatmapsets))\/(\d+)\/?\#?(\w+)?\/?(\d+)?\/?(?>[&,?].+=\w+)?\s?(?>\+(\w+))?", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public Func<ITelegramBotClient, Update, Task> action => new Func<ITelegramBotClient, Update, Task>(async (bot, update) =>
        {
            var message = update.Message;

            var user = Variables.osuUsers.FirstOrDefault(m => m.telegramId == message.From.Id);
            var chat = Variables.chats.First(m => m.chat.Id == message.Chat.Id);

            ILocalization language = Localization.Localization.Methods.GetLang(chat.language);

            var parsedBeatmap = BeatmapLinkParser.Parse(message.Text);
            var parsedProfile = ProfileLinkParser.Parse(message.Text);

            if (parsedBeatmap != null)
            {
                Beatmap beatmap = await parsedBeatmap.Parse();
                int beatmap_id = int.Parse(beatmap.beatmap_id);
                string textToSend = Localization.Localization.Methods.ReplaceEmpty(language.send_mapInfo, new[] { $"{beatmap.version}", $"{double.Parse(beatmap.difficultyrating):N2}", $"{parsedBeatmap.duration}", $"{beatmap.creator}", $"{beatmap.GetApproved()}", $"{beatmap.diff_size()}", $"{beatmap.diff_approach()}", $"{beatmap.diff_overall()}", $"{beatmap.diff_drain()}", $"{beatmap.bpm()}", $"{parsedBeatmap.acc100[0]}", $"{parsedBeatmap.acc98[0]}", $"{parsedBeatmap.acc96[0]}", $"{parsedBeatmap.mods}" });

                InputOnlineFile photo = new InputOnlineFile(new Uri($"https://assets.ppy.sh/beatmaps/{beatmap.beatmapset_id}/covers/card@2x.jpg"));
                var ik = new InlineKeyboardMarkup(new InlineKeyboardButton("Song preview") { CallbackData = $"{chat.chat.Id} songpreview {beatmap.beatmapset_id}" });
                try
                {
                    await bot.SendPhotoAsync(message.Chat.Id, photo, caption: textToSend, ParseMode.Html, replyMarkup: ik);
                }
                catch
                {
                    await bot.SendTextMessageAsync(message.Chat.Id, textToSend, ParseMode.Html, replyMarkup: ik, disableWebPagePreview: true);
                }

                chat.lastBeatmap_id = beatmap_id;
                //Variables.db.InsertOrUpdateOsuChatsTable(chat, false);
            }
            else if (parsedProfile != null)
            {
                osu.V1.Types.User osuUser = await parsedProfile.Parse();

                string textToSend = Localization.Localization.Methods.ReplaceEmpty(language.command_user, new[] { "Standard", $"{osuUser.profile_url()}", $"{osuUser.username()}", $"{osuUser.pp_rank()}", $"{osuUser.pp_country_rank()}", $"{osuUser.country()}", $"{osuUser.pp_raw():N2}", $"{parsedProfile.different:N2}", $"{double.Parse(osuUser.accuracy()):N2}", $"{osuUser.playcount()}", $"{osuUser.playtime_hours()}", $"{osuUser.count_rank_ssh()}", $"{osuUser.count_rank_sh()}", $"{osuUser.count_rank_ss()}", $"{osuUser.count_rank_s()}", $"{osuUser.count_rank_a()}" });
                var ik = new InlineKeyboardMarkup(new InlineKeyboardButton[][]
                    {
                            new InlineKeyboardButton[] {new InlineKeyboardButton("Standard") {CallbackData = $"{message.Chat.Id} user 0 {osuUser.username()}"}, new InlineKeyboardButton("Taiko") {CallbackData = $"{message.Chat.Id} user 1 {osuUser.username()}" }},
                            new InlineKeyboardButton[] {new InlineKeyboardButton("Catch") {CallbackData = $"{message.Chat.Id} user 2 {osuUser.username()}" }, new InlineKeyboardButton("Mania") { CallbackData = $"{message.Chat.Id} user 3 {osuUser.username()}" }}
                    });

                await bot.SendTextMessageAsync(message.Chat.Id, textToSend, ParseMode.Html, replyMarkup: ik, disableWebPagePreview: true, replyToMessageId: message.MessageId);
            }
        });

        //public async void BeatmapLink(ITelegramBotClient bot, Update update)
        //{
        //    Message message = update.Message;
        //    var user = Variables.osuUsers.FirstOrDefault(m => m.telegramId == message.From.Id);
        //    var chat = Variables.chats.FirstOrDefault(m => m.chat.Id == message.Chat.Id);
        //    ILocalization language = Localization.Methods.GetLang(chat.language);

        //    long beatmap_id = -1;
        //    string[] splittedLink = message.Text.Split("/");
        //    string last = splittedLink.Last();
        //    int index = last.IndexOf('+');
        //    string mods = "";
        //    OppaiSharp.Mods Mods = new OppaiSharp.Mods();
        //    if (index != -1)
        //    {
        //        mods = last.Substring(index + 1, last.Length - index - 1);
        //        for (int i = 0; i < mods.Length - 1; i += 2)
        //        {
        //            string mod = mods.Substring(i, 2).ToUpper();
        //            Mods = (OppaiSharp.Mods)Variables.osuApi.getMod((Sosu.osu.V1.Enums.Mods)Mods, ref mod);
        //        }
        //        splittedLink = message.Text.Replace("+" + mods, "").Split('/');
        //    }
        //    else
        //    {
        //        Mods = OppaiSharp.Mods.NoMod;
        //    }

        //    var array = splittedLink.Reverse().ToArray();
        //    bool isBeatmapSets = false;
        //    for (int i = 0; i <= array.Count() - 1; i++)
        //    {
        //        try
        //        {
        //            beatmap_id = long.Parse(array[i]);
        //            if (array[i + 1] == "beatmapsets")
        //                isBeatmapSets = true;
        //            break;
        //        }
        //        catch (Exception)
        //        {
        //            continue;
        //        }
        //    }


        //    if (beatmap_id < 0) return;

        //    Beatmap beatmap = null;
        //    if (!isBeatmapSets)
        //        beatmap = await Variables.osuApi.GetBeatmapByBeatmapIdAsync(beatmap_id, (int)(Mods));
        //    else
        //    {
        //        beatmap = await Variables.osuApi.GetBeatmapByBeatmapsetsIdAsync(beatmap_id, 0, (int)(Mods));
        //        beatmap_id = int.Parse(beatmap.beatmap_id);
        //    }
        //    double[] acc100 = await Other.ppCalc(beatmap_id, 100, Mods, 0, int.Parse(beatmap.max_combo));
        //    double[] acc98 = await Other.ppCalc(beatmap_id, 98, Mods, 0, int.Parse(beatmap.max_combo));
        //    double[] acc96 = await Other.ppCalc(beatmap_id, 96, Mods, 0, int.Parse(beatmap.max_combo));


        //    string duration = $"{beatmap.hit_length() / 60}:{(beatmap.hit_length() % 60):00}";
        //    string textToSend = Langs.ReplaceEmpty(language.send_mapInfo(), new[] { $"{beatmap.version}", $"{double.Parse(beatmap.difficultyrating):N2}", $"{duration}", $"{beatmap.creator}", $"{beatmap.GetApproved()}", $"{beatmap.diff_size()}", $"{beatmap.diff_approach()}", $"{beatmap.diff_overall()}", $"{beatmap.diff_drain()}", $"{beatmap.bpm()}", $"{acc100[0]}", $"{acc98[0]}", $"{acc96[0]}", $"{Mods}" });
        //    InputOnlineFile photo = new InputOnlineFile(new Uri($"https://assets.ppy.sh/beatmaps/{beatmap.beatmapset_id}/covers/card@2x.jpg"));

        //    chat.lastBeatmap_id = beatmap_id;
        //    Variables.db.InsertOrUpdateOsuChatsTable(chat, false);

        //    var ik = new InlineKeyboardMarkup(new InlineKeyboardButton("Song prewiew") { CallbackData = $"{chat.chat.Id} songprewiew {beatmap.beatmapset_id}" });
        //    try
        //    {
        //        await bot.SendPhotoAsync(message.Chat.Id, photo, caption: textToSend, ParseMode.Html, replyMarkup: ik);
        //    }
        //    catch (Exception)
        //    {
        //        await bot.SendTextMessageAsync(message.Chat.Id, textToSend, ParseMode.Html, replyMarkup: ik, disableWebPagePreview: true);
        //    }
        //}
    }
}
