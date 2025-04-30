using Microsoft.EntityFrameworkCore;
using OsuApi.Core.V2.Scores.Models;
using OsuApi.Core.V2.Users.Models;
using Sosu.Localization;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.Helpers;
using SosuBot.OsuTypes;
using SosuBot.Services.Handlers.Commands;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace SosuBot.Services.Handlers.Callbacks
{
    public class OsuUserBestCallbackCommand : CommandBase<CallbackQuery>
    {
        public static string Command = "userbest";

        public override async Task ExecuteAsync()
        {
            ILocalization language = new Russian();

            string[] parameters = Context.Data!.Split(' ');
            long chatId = long.Parse(parameters[0]);
            string directionOfPaging = parameters[2];
            int page = int.Parse(parameters[3]);
            Playmode playmode = (Playmode)int.Parse(parameters[4]);
            string osuUsername = string.Join(' ', parameters[5..]);

            TelegramChat? chatInDatabase = await Database.TelegramChats.FindAsync(chatId);
            OsuUser? osuUserInDatabase = await Database.OsuUsers.FirstOrDefaultAsync(u => u.OsuUsername == osuUsername);

            if (directionOfPaging == "previous" && page == 0 || osuUserInDatabase is null)
            {
                return;
            }

            Score[] scores;

            if (directionOfPaging == "next")
            {
                int offset = 5 * (page + 1);
                scores = (await OsuApiV2.Users.GetUserScores(osuUserInDatabase.OsuUserId, ScoreType.Best, new() { Mode = playmode.ToRuleset(), Limit = 5, Offset = offset }))!.Scores;
                page += 1;
            }
            else if (directionOfPaging == "previous")
            {
                int offset = 5 * (page - 1);
                scores = (await OsuApiV2.Users.GetUserScores(osuUserInDatabase.OsuUserId, ScoreType.Best, new() { Mode = playmode.ToRuleset(), Limit = 5, Offset = offset }))!.Scores;
                page -= 1;
            }
            else throw new NotImplementedException();

            BeatmapExtended[] beatmaps = scores.Select(async score => await OsuApiV2.Beatmaps.GetBeatmap((long)score.Beatmap!.Id)).Select(t => t.Result!.BeatmapExtended).ToArray()!;

            string textToSend = $"{osuUsername}({playmode.ToGamemode()})\n\n";
            int index = page * 5;
            foreach (var score in scores)
            {
                var beatmap = beatmaps[index - page * 5];
                textToSend += language.command_userbest.Fill([
                    $"{index + 1}",
                    $"{score.Rank}",
                    $"{score.BeatmapId}",
                    $"{score.Beatmapset!.Title.EncodeHTML()}",
                    $"{score.Beatmap!.Version.EncodeHTML()}",
                    $"{score.Beatmapset.Status}",
                    $"{ScoreHelper.GetScoreStatisticsText(score.Statistics!, playmode)}",
                    $"{score.Statistics!.Miss}",
                    $"{score.Accuracy * 100:N2}",
                    $"{ScoreHelper.GetModsText(score.Mods!)}",
                    $"{score.MaxCombo}",
                    $"{beatmap.MaxCombo}",
                    $"{ScoreHelper.GetScorePPText(score.Pp)}"]);
                index += 1;
            }
            var ik = new InlineKeyboardMarkup(
                    new InlineKeyboardButton[] { new InlineKeyboardButton("Previous") { CallbackData = $"{chatId} userbest previous {page} {(int)playmode} {osuUsername}" },
                    new InlineKeyboardButton("Next") { CallbackData = $"{chatId} userbest next {page} {(int)playmode} {osuUsername}" } }
               );

            await Context.Message!.EditAsync(BotClient, textToSend, replyMarkup: ik);
        }
    }
}
