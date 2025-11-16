using Microsoft.Extensions.DependencyInjection;
using OsuApi.V2;
using OsuApi.V2.Clients.Beatmaps.HttpIO;
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
    public static readonly string Command = "userbest";
    private ApiV2 _osuApiV2 = null!;

    public override Task BeforeExecuteAsync()
    {
        _osuApiV2 = Context.ServiceProvider.GetRequiredService<ApiV2>();
        return Task.CompletedTask;
    }

    public override async Task ExecuteAsync()
    {
        await BeforeExecuteAsync();

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
        int offset;

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

        userScoreResponse = (await _osuApiV2.Users.GetUserScores(osuUserId, ScoreType.Best,
            new GetUserScoreQueryParameters { Mode = playmode.ToRuleset(), Limit = 5, Offset = offset }))!;
        scores = userScoreResponse.Scores;
        if (scores.Length == 0) return;

        var textToSend = $"{osuUsername}({playmode.ToGamemode()})\n\n";
        var index = page * 5;
        foreach (var score in scores)
        {
            // should be equal to the variant from OsuUserbestCommand
            string fcText = " (" + (score.IsPerfectCombo!.Value ? "PFC" : "notPFC") + ")";
            
            textToSend += language.command_userbest.Fill([
                $"{index + 1}",
                $"{ScoreHelper.GetScoreRankEmoji(score.Rank)}{ScoreHelper.ParseScoreRank(score.Rank!)}",
                $"{score.BeatmapId}",
                $"{score.Beatmapset!.Title.EncodeHtml()}",
                $"{score.Beatmap!.Version.EncodeHtml()}",
                $"{score.Beatmapset.Status}",
                $"{ScoreHelper.GetScoreStatisticsText(score.Statistics!, playmode)}",
                $"{score.Statistics!.Miss}",
                $"{ScoreHelper.GetFormattedNumConsideringNull(score.Accuracy * 100, round:false)}",
                $"{ScoreHelper.GetModsText(score.Mods!)}",
                $"{score.MaxCombo}",
                $"{fcText}",
                $"{ScoreHelper.GetFormattedNumConsideringNull(score.Pp)}"
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