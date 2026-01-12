using Microsoft.Extensions.DependencyInjection;
using OsuApi.V2;
using OsuApi.V2.Clients.Users.HttpIO;
using OsuApi.V2.Models;
using OsuApi.V2.Users.Models;
using SosuBot.Database;
using SosuBot.Extensions;
using SosuBot.Helpers.OutputText;
using SosuBot.Localization;
using SosuBot.Localization.Languages;
using SosuBot.Services.Handlers.Abstract;
using SosuBot.Services.Synchronization;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace SosuBot.Services.Handlers.Commands;

public sealed class OsuUserbestCommand : CommandBase<Message>
{
    public static readonly string[] Commands = ["/userbest", "/best"];
    private ApiV2 _osuApiV2 = null!;
    private ScoreHelper _scoreHelper = null!;
    private RateLimiterFactory _rateLimiterFactory = null!;
    private BotContext _database = null!;

    public override async Task BeforeExecuteAsync()
    {
        await base.BeforeExecuteAsync();
        _osuApiV2 = Context.ServiceProvider.GetRequiredService<ApiV2>();
        _rateLimiterFactory = Context.ServiceProvider.GetRequiredService<RateLimiterFactory>();
        _scoreHelper = Context.ServiceProvider.GetRequiredService<ScoreHelper>();
        _database = Context.ServiceProvider.GetRequiredService<BotContext>();
    }

    public override async Task ExecuteAsync()
    {
        var rateLimiter = _rateLimiterFactory.Get(RateLimiterFactory.RateLimitPolicy.Command);
        if (!await rateLimiter.IsAllowedAsync($"{Context.Update.From!.Id}"))
        {
            await Context.Update.ReplyAsync(Context.BotClient, "Давай не так быстро!");
            return;
        }

        ILocalization language = new Russian();
        var chatInDatabase = await _database.TelegramChats.FindAsync(Context.Update.Chat.Id);
        var osuUserInDatabase = await _database.OsuUsers.FindAsync(Context.Update.From!.Id);

        var waitMessage = await Context.Update.ReplyAsync(Context.BotClient, language.waiting);

        // Fake 500ms wait
        await Task.Delay(500);

        Score[] bestScores;
        string osuUsernameForUserbest;
        long osuUserIdForUserbest;
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
        }
        else
        {
            var rulesetAlreadySet = false;
            if (parameters.Length == 2)
            {
                ruleset = parameters[1].ParseToRuleset()!;
                rulesetAlreadySet = true;
            }

            var userResponse = await _osuApiV2.Users.GetUser(parameters[0], new GetUserQueryParameters());
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
        }

        bestScores = (await _osuApiV2.Users.GetUserScores(osuUserIdForUserbest, ScoreType.Best,
            new GetUserScoreQueryParameters { Limit = 5, Mode = ruleset }))!.Scores;

        if (bestScores.Length == 0)
        {
            await waitMessage.EditAsync(Context.BotClient, language.error_noBestScores);
            return;
        }

        var gamemode = ruleset.ParseRulesetToGamemode();
        var playmode = ruleset.ParseRulesetToPlaymode();
        var textToSend = $"{UserHelper.GetUserProfileUrlWrappedInUsernameString((int)osuUserIdForUserbest, osuUsernameForUserbest)} (<b>{gamemode}</b>)\n\n";

        for (var i = 0; i <= bestScores.Length - 1; i++)
        {
            var score = bestScores[i];
            string fcText = " (" + (score.IsPerfectCombo!.Value ? "PFC" : "notPFC") + ")";

            textToSend += language.command_userbest.Fill([
                $"{i + 1}",
                $"{_scoreHelper.GetScoreRankEmoji(score.Rank)}{_scoreHelper.ParseScoreRank(score.Rank!)}",
                $"{score.BeatmapId}",
                $"{score.Beatmapset!.Title.EncodeHtml()}",
                $"{score.Beatmap!.Version.EncodeHtml()}",
                $"{score.Beatmapset.Status}",
                $"{_scoreHelper.GetScoreStatisticsText(score.Statistics!, playmode)}",
                $"{score.Statistics!.Miss}",
                $"{_scoreHelper.GetFormattedNumConsideringNull(score.Accuracy * 100, round: false)}",
                $"{_scoreHelper.GetModsText(score.Mods!)}",
                $"{score.MaxCombo}",
                $"{fcText}",
                $"{_scoreHelper.GetFormattedNumConsideringNull(score.Pp)}"
            ]);
        }

        var ik = new InlineKeyboardMarkup(
            new InlineKeyboardButton("Previous")
            {
                CallbackData =
                    $"userbest previous 0 {(int)playmode} {osuUserIdForUserbest} {osuUsernameForUserbest}"
            },
            new InlineKeyboardButton("Next")
            {
                CallbackData =
                    $"userbest next 0 {(int)playmode} {osuUserIdForUserbest} {osuUsernameForUserbest}"
            });
        await waitMessage.EditAsync(Context.BotClient, textToSend, replyMarkup: ik);
    }
}