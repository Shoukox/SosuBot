using OsuApi.V2.Clients.Users.HttpIO;
using OsuApi.V2.Models;
using OsuApi.V2.Users.Models;
using SosuBot.Extensions;
using SosuBot.Helpers.OutputText;
using SosuBot.Helpers.Types;
using SosuBot.Localization;
using SosuBot.Localization.Languages;
using SosuBot.Services.Handlers.Abstract;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace SosuBot.Services.Handlers.Callbacks;

public class OsuUserBestCallbackCommand : CommandBase<CallbackQuery>
{
    public static string Command = "userbest";

    public override async Task ExecuteAsync()
    {
        ILocalization language = new Russian();

        var parameters = Context.Update.Data!.Split(' ');
        var chatId = long.Parse(parameters[0]);
        var directionOfPaging = parameters[2];
        var page = int.Parse(parameters[3]);
        var playmode = (Playmode)int.Parse(parameters[4]);
        var osuUserId = long.Parse(parameters[5]);
        var osuUsername = string.Join(" ", parameters[6..]);

        Score[] scores;
        GetUserScoresResponse userScoreResponse;
        var offset = -1;

        if (directionOfPaging == "next")
        {
            offset = 5 * (page + 1);
            page += 1;
        }
        else if (directionOfPaging == "previous")
        {
            if (page == 0) return;
            offset = 5 * (page - 1);
            page -= 1;
        }
        else
        {
            throw new NotImplementedException();
        }

        userScoreResponse = (await Context.OsuApiV2.Users.GetUserScores(osuUserId, ScoreType.Best,
            new GetUserScoreQueryParameters { Mode = playmode.ToRuleset(), Limit = 5, Offset = offset }))!;
        scores = userScoreResponse.Scores;
        if (scores.Length == 0) return;

        BeatmapExtended[] beatmaps = scores
            .Select(async score => await Context.OsuApiV2.Beatmaps.GetBeatmap((long)score.Beatmap!.Id))
            .Select(t => t.Result!.BeatmapExtended).ToArray()!;

        var textToSend = $"{osuUsername}({playmode.ToGamemode()})\n\n";
        var index = page * 5;
        foreach (var score in scores)
        {
            var beatmap = beatmaps[index - page * 5];

            // should be equal to the variant from OsuUserbestCommand
            textToSend += language.command_userbest.Fill([
                $"{index + 1}",
                $"{ScoreHelper.GetScoreRankEmoji(score.Rank)}{score.Rank}",
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
                $"{ScoreHelper.GetFormattedPpTextConsideringNull(score.Pp)}"
            ]);
            index += 1;
        }

        var ik = new InlineKeyboardMarkup(
            new InlineKeyboardButton("Previous")
                { CallbackData = $"{chatId} userbest previous {page} {(int)playmode} {osuUserId} {osuUsername}" },
            new InlineKeyboardButton("Next")
                { CallbackData = $"{chatId} userbest next {page} {(int)playmode} {osuUserId} {osuUsername}" });

        try
        {
            await Context.Update.Message!.EditAsync(Context.BotClient, textToSend, replyMarkup: ik);
        }
        catch (ApiRequestException e) when (e.ErrorCode == 400)
        {
            await Context.Update.AnswerAsync(Context.BotClient);
        }
    }
}