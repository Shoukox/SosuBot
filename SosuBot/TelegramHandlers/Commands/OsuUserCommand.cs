using Microsoft.Extensions.DependencyInjection;
using OsuApi.BanchoV2;
using OsuApi.BanchoV2.Clients.Users.HttpIO;
using OsuApi.BanchoV2.Users.Models;
using SosuBot.Database;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.Helpers;
using SosuBot.Services.Synchronization;
using SosuBot.TelegramHandlers.Abstract;
using Telegram.Bot.Types;

namespace SosuBot.TelegramHandlers.Commands;

public class OsuUserCommand(bool includeIdInSearch = false) : CommandBase<Message>
{
    public static readonly string[] Commands = ["/user", "/u"];
    private BanchoApiV2 _osuApiV2 = null!;
    private ScoreHelper _scoreHelper = null!;
    private RateLimiterFactory _rateLimiterFactory = null!;
    private BotContext _database = null!;

    public override async Task BeforeExecuteAsync()
    {
        await base.BeforeExecuteAsync();
        _scoreHelper = Context.ServiceProvider.GetRequiredService<ScoreHelper>();
        _osuApiV2 = Context.ServiceProvider.GetRequiredService<BanchoApiV2>();
        _rateLimiterFactory = Context.ServiceProvider.GetRequiredService<RateLimiterFactory>();
        _database = Context.ServiceProvider.GetRequiredService<BotContext>();
    }

    public override async Task ExecuteAsync()
    {
        var language = Context.GetLocalization();
        var rateLimiter = _rateLimiterFactory.Get(RateLimiterFactory.RateLimitPolicy.Command);
        if (!await rateLimiter.IsAllowedAsync($"{Context.Update.From!.Id}"))
        {
            await Context.Update.ReplyAsync(Context.BotClient, language.common_rateLimitSlowDown);
            return;
        }


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

        int achievementsCount = user.UserAchievements?.Length ?? 0;
        var achievementsTotalText = $"{OsuConstants.TotalAchievementsCount} ({(double)achievementsCount / OsuConstants.TotalAchievementsCount * 100:00.00}%)";

        var textToSend = LocalizationMessageHelper.UserProfileText(
            language,
            _scoreHelper,
            user,
            playmode,
            currentPp,
            ppDifferenceText,
            achievementsTotalText);

        await waitMessage.EditAsync(Context.BotClient, textToSend, replyMarkup: UserHelper.BuildUserModeKeyboard(user.Username!));
    }
}




