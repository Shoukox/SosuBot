using OsuApi.V2.Clients.Beatmaps.HttpIO;
using OsuApi.V2.Clients.Users.HttpIO;
using OsuApi.V2.Models;
using OsuApi.V2.Users.Models;
using SosuBot.Extensions;
using SosuBot.Helpers.OutputText;
using SosuBot.Localization;
using SosuBot.Localization.Languages;
using SosuBot.Services.Handlers.Abstract;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace SosuBot.Services.Handlers.Commands;

public class OsuUserbestCommand : CommandBase<Message>
{
    public static string[] Commands = ["/userbest", "/best"];

    public override async Task ExecuteAsync()
    {
        if (await Context.Update.IsUserSpamming(Context.BotClient))
            return;

        ILocalization language = new Russian();
        var chatInDatabase = await Context.Database.TelegramChats.FindAsync(Context.Update.Chat.Id);
        var osuUserInDatabase = await Context.Database.OsuUsers.FindAsync(Context.Update.From!.Id);

        var waitMessage = await Context.Update.ReplyAsync(Context.BotClient, language.waiting);

        Score[] bestScores;
        var osuUsernameForUserbest = string.Empty;
        long osuUserIdForUserbest = -1;
        var ruleset = Ruleset.Osu;

        var parameters = Context.Update.Text!.GetCommandParameters()!;
        if (parameters.Length == 0)
        {
            if (osuUserInDatabase is null)
            {
                await waitMessage.EditAsync(Context.BotClient, language.error_userNotSetHimself);
                return;
            }

            ruleset = osuUserInDatabase.OsuMode.ToRuleset();
            osuUsernameForUserbest = osuUserInDatabase.OsuUsername;
            osuUserIdForUserbest = osuUserInDatabase.OsuUserId;
            bestScores = (await Context.OsuApiV2.Users.GetUserScores(osuUserInDatabase.OsuUserId, ScoreType.Best,
                new GetUserScoreQueryParameters { Limit = 5, Mode = ruleset }))!.Scores;
        }
        else
        {
            var rulesetAlreadySet = false;
            if (parameters.Length == 2)
            {
                ruleset = parameters[1].ParseToRuleset()!;
                rulesetAlreadySet = true;
            }

            var userResponse = await Context.OsuApiV2.Users.GetUser(parameters[0], new GetUserQueryParameters());
            if (userResponse is null)
            {
                await waitMessage.EditAsync(Context.BotClient,
                    language.error_specificUserNotFound.Fill([parameters[0]]) + "\n\n" +
                    language.error_hintReplaceSpaces);
                return;
            }

            if (!rulesetAlreadySet) ruleset = userResponse.UserExtend!.Playmode!;
            osuUsernameForUserbest = userResponse.UserExtend!.Username!;
            osuUserIdForUserbest = userResponse.UserExtend!.Id.Value;
            var userbestResponse = await Context.OsuApiV2.Users.GetUserScores(userResponse.UserExtend!.Id.Value,
                ScoreType.Best, new GetUserScoreQueryParameters { Limit = 5, Mode = ruleset });
            bestScores = userbestResponse!.Scores;
        }

        if (bestScores.Length == 0)
        {
            await waitMessage.EditAsync(Context.BotClient, language.error_noBestScores);
            return;
        }

        var gamemode = ruleset.ParseRulesetToGamemode();
        var playmode = ruleset.ParseRulesetToPlaymode();
        var textToSend = $"{osuUsernameForUserbest} (<b>{gamemode}</b>)\n\n";

        GetBeatmapResponse[] beatmaps = bestScores
            .Select(async score => await Context.OsuApiV2.Beatmaps.GetBeatmap((long)score.Beatmap!.Id))
            .Select(t => t.Result).ToArray()!;
        for (var i = 0; i <= bestScores.Length - 1; i++)
        {
            var score = bestScores[i];
            var beatmap = beatmaps[i];
            textToSend += language.command_userbest.Fill([
                $"{i + 1}",
                $"{ScoreHelper.GetScoreRankEmoji(score.Rank)}{ScoreHelper.ParseScoreRank(score.Rank!)}",
                $"{score.BeatmapId}",
                $"{score.Beatmapset!.Title.EncodeHtml()}",
                $"{score.Beatmap!.Version.EncodeHtml()}",
                $"{score.Beatmapset.Status}",
                $"{ScoreHelper.GetScoreStatisticsText(score.Statistics!, playmode)}",
                $"{score.Statistics!.Miss}",
                $"{score.Accuracy * 100:N2}",
                $"{ScoreHelper.GetModsText(score.Mods!)}",
                $"{score.MaxCombo}",
                $"{beatmap.BeatmapExtended!.MaxCombo}",
                $"{ScoreHelper.GetFormattedPpTextConsideringNull(score.Pp)}"
            ]);
        }

        var ik = new InlineKeyboardMarkup(
            new InlineKeyboardButton("Previous")
            {
                CallbackData =
                    $"{chatInDatabase!.ChatId} userbest previous 0 {(int)playmode} {osuUserIdForUserbest} {osuUsernameForUserbest}"
            },
            new InlineKeyboardButton("Next")
            {
                CallbackData =
                    $"{chatInDatabase.ChatId} userbest next 0 {(int)playmode} {osuUserIdForUserbest} {osuUsernameForUserbest}"
            });
        await waitMessage.EditAsync(Context.BotClient, textToSend, replyMarkup: ik);
    }
}