using OsuApi.Core.V2.Beatmaps.Models.HttpIO;
using OsuApi.Core.V2.Scores.Models;
using OsuApi.Core.V2.Users.Models;
using Sosu.Localization;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.Helpers;
using SosuBot.OsuTypes;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace SosuBot.Services.Handlers.Commands.MessageCommands
{
    public class OsuUserbestCommand : CommandBase<Message>
    {
        public static string[] Commands = ["/userbest", "/best"];

        public override async Task ExecuteAsync()
        {
            ILocalization language = new Russian();
            TelegramChat? chatInDatabase = await Database.TelegramChats.FindAsync(Context.Chat.Id);
            OsuUser? osuUserInDatabase = await Database.OsuUsers.FindAsync(Context.From!.Id);

            Message waitMessage = await Context.ReplyAsync(BotClient, language.waiting);

            Score[] bestScores;
            string osuUsernameForUserbest = string.Empty;
            string ruleset = Ruleset.Osu;

            string[] parameters = Context.Text!.GetCommandParameters()!;
            if (parameters.Length == 0)
            {
                if (osuUserInDatabase is null)
                {
                    await waitMessage.EditAsync(BotClient, language.error_noUser);
                    return;
                }
                ruleset = osuUserInDatabase.OsuMode.ToRuleset();
                osuUsernameForUserbest = osuUserInDatabase.OsuUsername;
                bestScores = (await OsuApiV2.Users.GetUserScores(osuUserInDatabase.OsuUserId, ScoreType.Best, new() { Limit = 5, Mode = ruleset }))!.Scores;
            }
            else
            {
                if (parameters.Length == 2)
                {
                    ruleset = parameters[2].ParseToRuleset()!;
                }

                var userResponse = await OsuApiV2.Users.GetUser(parameters[0], new());
                if (userResponse is null)
                {
                    await waitMessage.EditAsync(BotClient, language.error_noUser + "\n\n" + language.error_hintReplaceSpaces);
                    return;
                }

                ruleset = userResponse.UserExtend!.Playmode!;
                osuUsernameForUserbest = userResponse.UserExtend!.Username!;
                var userbestResponse = await OsuApiV2.Users.GetUserScores(userResponse.UserExtend!.Id.Value, ScoreType.Best, new() { Limit = 5, Mode = ruleset });
                bestScores = userbestResponse!.Scores;
            }

            if (bestScores.Length == 0)
            {
                await waitMessage.EditAsync(BotClient, language.error_noBestScores);
                return;
            }

            string gamemode = ruleset.ParseRulesetToGamemode();
            Playmode playmode = ruleset.ParseRulesetToPlaymode();
            string textToSend = $"{osuUsernameForUserbest} (<b>{gamemode}</b>)\n\n";

            GetBeatmapResponse[] beatmaps = bestScores.Select(async score => await OsuApiV2.Beatmaps.GetBeatmap((long)score.Beatmap!.Id)).Select(t => t.Result).ToArray()!;
            for (int i = 0; i <= bestScores.Length - 1; i++)
            {
                var score = bestScores[i];
                var beatmap = beatmaps[i];
                textToSend += language.command_userbest.Fill([
                    $"{i + 1}",
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
                    $"{beatmap.BeatmapExtended!.MaxCombo}",
                    $"{ScoreHelper.GetScorePPText(score.Pp)}"]);
            }

            var ik = new InlineKeyboardMarkup(
                new InlineKeyboardButton[] { new InlineKeyboardButton("Previous") { CallbackData = $"{chatInDatabase!.ChatId} userbest previous 0 {(int)playmode} {osuUsernameForUserbest}" }, new InlineKeyboardButton("Next") { CallbackData = $"{chatInDatabase.ChatId} userbest next 0 {(int)playmode} {osuUsernameForUserbest}" } }
            );
            await waitMessage.EditAsync(BotClient, textToSend, replyMarkup: ik);
        }
    }
}
