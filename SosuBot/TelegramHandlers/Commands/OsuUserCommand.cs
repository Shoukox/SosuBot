using Microsoft.Extensions.DependencyInjection;
using OsuApi.V2;
using OsuApi.V2.Clients.Users.HttpIO;
using OsuApi.V2.Users.Models;
using SosuBot.Database;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.Helpers;
using SosuBot.Helpers.OutputText;
using SosuBot.Localization;
using SosuBot.Localization.Languages;
using SosuBot.Services.Synchronization;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using SosuBot.TelegramHandlers.Abstract;

namespace SosuBot.TelegramHandlers.Commands;

public class OsuUserCommand(bool includeIdInSearch = false) : CommandBase<Message>
{
    public static readonly string[] Commands = ["/user", "/u"];
    private ApiV2 _osuApiV2 = null!;
    private ScoreHelper _scoreHelper = null!;
    private RateLimiterFactory _rateLimiterFactory = null!;
    private BotContext _database = null!;

    public override async Task BeforeExecuteAsync()
    {
        await base.BeforeExecuteAsync();
        _scoreHelper = Context.ServiceProvider.GetRequiredService<ScoreHelper>();
        _osuApiV2 = Context.ServiceProvider.GetRequiredService<ApiV2>();
        _rateLimiterFactory = Context.ServiceProvider.GetRequiredService<RateLimiterFactory>();
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
        var osuUserInDatabase = await _database.OsuUsers.FindAsync(Context.Update.From!.Id);
        var parameters = Context.Update.Text!.GetCommandParameters()!;

        var waitMessage = await Context.Update.ReplyAsync(Context.BotClient, language.waiting);

        // Fake 500ms wait
        await Task.Delay(500);

        UserExtend? user;
        Playmode playmode;

        var searchPrefix = "@";
        if (includeIdInSearch) searchPrefix = "";

        if (parameters.Length == 0)
        {
            if (osuUserInDatabase is null)
            {
                await waitMessage.EditAsync(Context.BotClient, language.error_userNotSetHimself);
                return;
            }

            playmode = osuUserInDatabase.OsuMode;
            user = (await _osuApiV2.Users.GetUser($"{searchPrefix}{osuUserInDatabase.OsuUsername}",
                new GetUserQueryParameters(), playmode.ToRuleset()))?.UserExtend;
        }
        else if (parameters.Length == 1)
        {
            if (parameters[0].StartsWith("mode="))
            {
                if (osuUserInDatabase is null)
                {
                    await waitMessage.EditAsync(Context.BotClient, language.error_userNotSetHimself);
                    return;
                }

                var ruleset = parameters[0].ParseToRuleset();
                if (ruleset is null)
                {
                    await waitMessage.EditAsync(Context.BotClient, language.error_modeIncorrect);
                    return;
                }

                var userResponse = await _osuApiV2.Users.GetUser($"{searchPrefix}{osuUserInDatabase.OsuUsername}",
                    new GetUserQueryParameters(), ruleset);
                if (userResponse is null)
                {
                    await waitMessage.EditAsync(Context.BotClient, language.error_userNotFound);
                    return;
                }

                user = userResponse.UserExtend;
            }
            else
            {
                var userResponse =
                    await _osuApiV2.Users.GetUser($"{searchPrefix}{parameters[0]}", new GetUserQueryParameters());
                if (userResponse is null)
                {
                    await waitMessage.EditAsync(Context.BotClient, language.error_userNotFound);
                    return;
                }

                user = userResponse.UserExtend;
            }

            playmode = user!.Playmode!.ParseRulesetToPlaymode();
        }
        else
        {
            await waitMessage.EditAsync(Context.BotClient, language.error_argsLength);
            return;
        }

        if (user == null)
        {
            await waitMessage.EditAsync(Context.BotClient, language.error_userNotFound);
            return;
        }

        double? currentPp = user.Statistics!.Pp;
        var ppDifferenceText =
            await UserHelper.GetPpDifferenceTextAsync(_database, user, playmode, currentPp);

        UserHelper.UpdateOsuUsers(_database, user, playmode);

        DateTime.TryParse(user.JoinDate?.Value, out var registerDateTime);
        var textToSend = language.command_user.Fill([
            $"{playmode.ToGamemode()}",
            $"{UserHelper.GetUserProfileUrlWrappedInUsernameString(user.Id.Value, user.Username!)}",
            $"{UserHelper.GetUserRankText(user.Statistics.GlobalRank)}",
            $"{UserHelper.GetUserRankText(user.Statistics.CountryRank)}",
            $"{UserHelper.CountryCodeToFlag(user.CountryCode ?? "nn")}",
            $"{_scoreHelper.GetFormattedNumConsideringNull(currentPp)}",
            $"{ppDifferenceText}",
            $"{user.Statistics.HitAccuracy:N2}%",
            $"{user.Statistics.PlayCount:N0}",
            $"{user.Statistics.PlayTime / 3600}",
            $"{registerDateTime:dd.MM.yyyy HH:mm:ss}",
            $"{user.UserAchievements?.Length ?? 0}",
            $"{OsuConstants.TotalAchievementsCount}",
            $"{user.Statistics.GradeCounts!.SSH}",
            $"{user.Statistics.GradeCounts!.SH}",
            $"{user.Statistics.GradeCounts!.SS}",
            $"{user.Statistics.GradeCounts!.S}",
            $"{user.Statistics.GradeCounts!.A}"
        ]);
        var ik = new InlineKeyboardMarkup(new InlineKeyboardButton[][]
        {
            [
                new InlineKeyboardButton("Standard") { CallbackData = $"user 0 {user.Username}" },
                new InlineKeyboardButton("Taiko") { CallbackData = $"user 1 {user.Username}" }
            ],
            [
                new InlineKeyboardButton("Catch") { CallbackData = $"user 2 {user.Username}" },
                new InlineKeyboardButton("Mania") { CallbackData = $"user 3 {user.Username}" }
            ]
        });

        await waitMessage.EditAsync(Context.BotClient, textToSend, replyMarkup: ik);
    }
}