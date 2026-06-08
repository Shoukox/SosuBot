using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OsuApi.BanchoV2;
using OsuApi.BanchoV2.Clients.Users.HttpIO;
using OsuApi.BanchoV2.Users.Models;
using SosuBot.Database;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.Helpers;
using SosuBot.Localization;
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
        ILocalization language = Context.GetLocalization();
        TokenBucketRateLimiter rateLimiter = _rateLimiterFactory.Get(RateLimiterFactory.RateLimitPolicy.Command);
        if (!await rateLimiter.IsAllowedAsync($"{Context.Update.From!.Id}"))
        {
            await Context.Update.ReplyAsync(Context.BotClient, language.common_rateLimitSlowDown);
            return;
        }

        OsuUser? osuUserInDatabase = await _database.OsuUsers.FindAsync(Context.Update.From!.Id);
        var keywordParameters = Context.Update.Text!.GetCommandKeywordParameters()!;
        var parameters = Context.Update.Text!.GetCommandParameters()!.Where(m => !keywordParameters.Contains(m)).ToArray();

        Message waitMessage = await Context.Update.ReplyAsync(Context.BotClient, language.waiting);

        // Fake 500ms wait
        await Task.Delay(500);

        string username;
        string? ruleset = null;
        Playmode playmode = Playmode.Osu;
        bool shouldUpdatePlaymode = false;

        string searchPrefix = "@";
        if (includeIdInSearch) searchPrefix = "";

        if (parameters.Length == 0)
        {
            if (osuUserInDatabase is null)
            {
                await waitMessage.EditAsync(Context.BotClient, language.error_userNotSetHimself);
                return;
            }

            playmode = osuUserInDatabase.OsuMode;
            username = $"{searchPrefix}{osuUserInDatabase.OsuUsername}";
            ruleset = playmode.ToRuleset();
        }
        else if (parameters.Length == 1)
        {
            if (parameters[0].StartsWith("@"))
            {
                await waitMessage.EditAsync(Context.BotClient, language.error_dontUseTelegramUsername);
                return;
            }

            username = $"{searchPrefix}{parameters[0]}";
            shouldUpdatePlaymode = true;
        }
        else
        {
            await waitMessage.EditAsync(Context.BotClient, language.error_argsLength);
            return;
        }

        if (ruleset == null || keywordParameters.Length != 0)
        {
            if (keywordParameters.FirstOrDefault(m => m.StartsWith("mode")) is { } keyword)
            {
                ruleset = keyword.Split('=')[1].ParseToRuleset();
                if (ruleset is null)
                {
                    await waitMessage.EditAsync(Context.BotClient, language.error_modeIncorrect);
                    return;
                }
            }
        }

        GetUserResponse? userResponse = await _osuApiV2.Users.GetUser(username, new GetUserQueryParameters(), ruleset);
        if (userResponse is null)
        {
            await waitMessage.EditAsync(Context.BotClient, language.error_userNotFound);
            return;
        }
        UserExtend? user = userResponse.UserExtend;
        if (user == null)
        {
            await waitMessage.EditAsync(Context.BotClient, language.error_userNotFound);
            return;
        }

        if (shouldUpdatePlaymode)
        {
            playmode = user!.Playmode!.ParseRulesetToPlaymode();
        }

        double? currentPp = user.Statistics!.Pp;
        var ppDifferenceText =
            await UserHelper.GetPpDifferenceTextAsync(_database, user, playmode, currentPp);

        UserHelper.UpdateOsuUsers(_database, user, playmode);

        int achievementsCount = user.UserAchievements?.Length ?? 0;
        var achievementsTotalText = $"{OsuConstants.TotalAchievementsCount} ({(double)achievementsCount / OsuConstants.TotalAchievementsCount * 100:#.00}%)";

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




