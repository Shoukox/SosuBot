using Microsoft.Extensions.DependencyInjection;
using OsuApi.BanchoV2;
using OsuApi.BanchoV2.Clients.Users.HttpIO;
using OsuApi.BanchoV2.Models;
using OsuApi.BanchoV2.Users.Models;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.Helpers.OutputText;
using SosuBot.Localization;
using SosuBot.Localization.Languages;
using SosuBot.TelegramHandlers.Abstract;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace SosuBot.TelegramHandlers.Callbacks;

public class OsuUserBestCallback : CommandBase<CallbackQuery>
{
    public static readonly string Command = "userbest";
    private BanchoApiV2 _osuApiV2 = null!;
    private ScoreHelper _scoreHelper = null!;

    public override async Task BeforeExecuteAsync()
    {
        await base.BeforeExecuteAsync();
        _osuApiV2 = Context.ServiceProvider.GetRequiredService<BanchoApiV2>();
        _scoreHelper = Context.ServiceProvider.GetRequiredService<ScoreHelper>();
    }

    public override async Task ExecuteAsync()
    {
        ILocalization language = new Russian();

        var parameters = Context.Update.Data!.Split(' ');
        var directionOfPaging = parameters[1];
        var page = int.Parse(parameters[2]);
        var playmode = (Playmode)int.Parse(parameters[3]);
        var osuUserId = long.Parse(parameters[4]);
        var osuUsername = string.Join(" ", parameters[5..]);

        var chatId = Context.Update.Message!.Chat.Id;

        Score[] scores;
        GetUserScoresResponse userScoreResponse;
        int offset = 0;

        if (directionOfPaging == "next")
        {
            offset = 5 * (page + 1);
            page += 1;
        }
        else if (directionOfPaging == "previous")
        {
            if (page == 0)
            {
                await Context.Update.AnswerAsync(Context.BotClient);
                return;
            }
            offset = 5 * (page - 1);
            page -= 1;
        }

        userScoreResponse = (await _osuApiV2.Users.GetUserScores(osuUserId, ScoreType.Best,
            new GetUserScoreQueryParameters { Mode = playmode.ToRuleset(), Limit = 5, Offset = offset }))!;
        scores = userScoreResponse.Scores;
        if (scores.Length == 0)
        {
            await Context.Update.AnswerAsync(Context.BotClient);
            return;
        }

        var textToSend = $"{UserHelper.GetUserProfileUrlWrappedInUsernameString((int)osuUserId, osuUsername)} (<b>{playmode.ToGamemode()}</b>)\n\n";
        var index = page * 5;
        foreach (var score in scores)
        {
            // should be equal to the variant from OsuUserbestCommand
            string fcText = " (" + (score.IsPerfectCombo!.Value ? "PFC" : "notPFC") + ")";

            textToSend += language.command_userbest.Fill([
                $"{index + 1}",
                $"{_scoreHelper.GetScoreRankEmoji(score.Rank)}{_scoreHelper.ParseScoreRank(score.Rank!)}",
                $"{score.BeatmapId}",
                $"{score.Beatmapset!.Title.EncodeHtml()}",
                $"{score.Beatmap!.Version.EncodeHtml()}",
                $"{score.Beatmapset.Status}",
                $"{_scoreHelper.GetScoreStatisticsText(score.Statistics!, playmode)}",
                $"{score.Statistics!.Miss}",
                $"{_scoreHelper.GetFormattedNumConsideringNull(score.Accuracy * 100, round:false)}",
                $"{_scoreHelper.GetModsText(score.Mods!)}",
                $"{score.MaxCombo}",
                $"{fcText}",
                $"{_scoreHelper.GetFormattedNumConsideringNull(score.Pp)}"
            ]);
            index += 1;
        }

        var ik = new InlineKeyboardMarkup(
            new InlineKeyboardButton("Previous")
            { CallbackData = $"userbest previous {page} {(int)playmode} {osuUserId} {osuUsername}" },
            new InlineKeyboardButton("Next")
            { CallbackData = $"userbest next {page} {(int)playmode} {osuUserId} {osuUsername}" });

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