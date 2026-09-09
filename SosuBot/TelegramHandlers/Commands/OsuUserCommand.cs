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
    public static readonly string Description = "[user] краткая информация об игроке";
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

        Message waitMessage = await Context.Update.ReplyAsync(Context.BotClient, language.waiting);

        // Fake 500ms wait
        await Task.Delay(500);

        OsuCommandArguments.Player? arguments = OsuCommandArguments.ParsePlayer(Context.Update.Text!);
        if (arguments is null)
        {
            await waitMessage.EditAsync(Context.BotClient,
                includeIdInSearch ? language.command_userId_usage : language.command_user_usage);
            return;
        }

        string username;
        string? ruleset = null;
        Playmode playmode = Playmode.Osu;

        string searchPrefix = "@";
        if (includeIdInSearch) searchPrefix = "";

        if (string.IsNullOrWhiteSpace(arguments.Username))
        {
            if (osuUserInDatabase is null)
            {
                await waitMessage.EditAsync(Context.BotClient,
                    includeIdInSearch ? language.command_userId_usage : language.command_user_usage);
                return;
            }

            playmode = arguments.Playmode ?? osuUserInDatabase.OsuMode;
            username = $"{searchPrefix}{osuUserInDatabase.OsuUsername}";
            ruleset = playmode.ToRuleset();
        }
        else
        {
            if (arguments.Username.StartsWith("@"))
            {
                await waitMessage.EditAsync(Context.BotClient, language.error_dontUseTelegramUsername);
                return;
            }

            username = $"{searchPrefix}{arguments.Username}";
            ruleset = arguments.Playmode?.ToRuleset();
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

        if (ruleset == null)
        {
            playmode = user!.Playmode!.ParseRulesetToPlaymode();
        }
        else
        {
            playmode = ruleset.ParseRulesetToPlaymode();
        }

        double? currentPp = user.Statistics!.Pp;
        var profileDifferenceTexts =
            await UserHelper.GetProfileDifferenceTextsAsync(_database, user, playmode);

        UserHelper.UpdateOsuUsers(_database, user, playmode);

        int achievementsCount = user.UserAchievements?.Length ?? 0;
        var achievementsTotalText = $"{OsuConstants.TotalAchievementsCount} ({(double)achievementsCount / OsuConstants.TotalAchievementsCount * 100:#.00}%)";

        var textToSend = LocalizationMessageHelper.UserProfileText(
            language,
            _scoreHelper,
            user,
            playmode,
            currentPp,
            profileDifferenceTexts.Pp,
            profileDifferenceTexts.Accuracy,
            profileDifferenceTexts.PlayTime,
            achievementsTotalText);

        await waitMessage.EditAsync(Context.BotClient, textToSend, replyMarkup: UserHelper.BuildUserModeKeyboard(user.Username!));
    }
}




