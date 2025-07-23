using OsuApi.V2.Clients.Users.HttpIO;
using OsuApi.V2.Models;
using OsuApi.V2.Users.Models;
using SosuBot.Extensions;
using SosuBot.Helpers.OsuTypes;
using SosuBot.Helpers.Scoring;
using SosuBot.Localization;
using SosuBot.Localization.Languages;
using SosuBot.Services.Handlers.Abstract;
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

            string[] parameters = Context.Update.Data!.Split(' ');
            long chatId = long.Parse(parameters[0]);
            string directionOfPaging = parameters[2];
            int page = int.Parse(parameters[3]);
            Playmode playmode = (Playmode)int.Parse(parameters[4]);
            long osuUserId = long.Parse(parameters[5]);
            string osuUsername = string.Join(" ", parameters[6..]);

            Score[] scores;
            GetUserScoresResponse userScoreResponse;
            int offset = -1;

            if (directionOfPaging == "next")
            {
                offset = 5 * (page + 1);
                page += 1;
            }
            else if (directionOfPaging == "previous")
            {
                if (page == 0)
                {
                    return;
                }
                offset = 5 * (page - 1);
                page -= 1;
            }
            else throw new NotImplementedException();

            userScoreResponse = (await Context.OsuApiV2.Users.GetUserScores(osuUserId, ScoreType.Best, new() { Mode = playmode.ToRuleset(), Limit = 5, Offset = offset }))!;
            scores = userScoreResponse.Scores;
            if (scores.Length == 0)
            {
                return;
            }

            BeatmapExtended[] beatmaps = scores.Select(async score => await Context.OsuApiV2.Beatmaps.GetBeatmap((long)score.Beatmap!.Id)).Select(t => t.Result!.BeatmapExtended).ToArray()!;

            string textToSend = $"{osuUsername}({playmode.ToGamemode()})\n\n";
            int index = page * 5;
            foreach (var score in scores)
            {
                var beatmap = beatmaps[index - page * 5];
                textToSend += language.command_userbest.Fill([
                    $"{index + 1}",
                    $"{score.Rank}",
                    $"{score.BeatmapId}",
                    $"{score.Beatmapset!.Title.EncodeHtml()}",
                    $"{score.Beatmap!.Version.EncodeHtml()}",
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
                    new InlineKeyboardButton[] { new InlineKeyboardButton("Previous") { CallbackData = $"{chatId} userbest previous {page} {(int)playmode} {osuUserId} {osuUsername}" },
                    new InlineKeyboardButton("Next") { CallbackData = $"{chatId} userbest next {page} {(int)playmode} {osuUserId} {osuUsername}" } }
               );
            await Context.Update.Message!.EditAsync(Context.BotClient, textToSend, replyMarkup: ik);
        }
    }
}
