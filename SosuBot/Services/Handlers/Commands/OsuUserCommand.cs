using OsuApi.V2.Clients.Users.HttpIO;
using OsuApi.V2.Users.Models;
using SosuBot.Extensions;
using SosuBot.Helpers.OutputText;
using SosuBot.Helpers.Scoring;
using SosuBot.Helpers.Types;
using SosuBot.Localization;
using SosuBot.Localization.Languages;
using SosuBot.Services.Handlers.Abstract;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace SosuBot.Services.Handlers.Commands;

public class OsuUserCommand : CommandBase<Message>
{
    public static string[] Commands = ["/user", "/u"];

    public override async Task ExecuteAsync()
    {
        if (await Context.Update.IsUserSpamming(Context.BotClient))
            return;

        ILocalization language = new Russian();
        var osuUserInDatabase = await Context.Database.OsuUsers.FindAsync(Context.Update.From!.Id);
        var parameters = Context.Update.Text!.GetCommandParameters()!;

        var waitMessage = await Context.Update.ReplyAsync(Context.BotClient, language.waiting);

        UserExtend user;
        var playmode = Playmode.Osu;

        if (parameters.Length == 0)
        {
            if (osuUserInDatabase is null)
            {
                await waitMessage.EditAsync(Context.BotClient, language.error_userNotSetHimself);
                return;
            }

            playmode = osuUserInDatabase.OsuMode;
            user = (await Context.OsuApiV2.Users.GetUser($"@{osuUserInDatabase.OsuUsername}",
                new GetUserQueryParameters(), playmode.ToRuleset()))!.UserExtend!;
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

                playmode = ruleset.ParseRulesetToPlaymode();

                var userResponse = await Context.OsuApiV2.Users.GetUser($"@{osuUserInDatabase.OsuUsername}",
                    new GetUserQueryParameters(), ruleset);
                if (userResponse is null)
                {
                    await waitMessage.EditAsync(Context.BotClient, language.error_userNotFound);
                    return;
                }

                user = userResponse.UserExtend!;
            }
            else
            {
                var userResponse =
                    await Context.OsuApiV2.Users.GetUser($"@{parameters[0]}", new GetUserQueryParameters());
                if (userResponse is null)
                {
                    await waitMessage.EditAsync(Context.BotClient, language.error_userNotFound);
                    return;
                }

                user = userResponse.UserExtend!;

                playmode = user.Playmode!.ParseRulesetToPlaymode();
            }
        }
        else
        {
            await waitMessage.EditAsync(Context.BotClient, language.error_argsLength);
            return;
        }

        double? savedPPInDatabase = null;
        double? currentPP = user.Statistics!.Pp;
        var ppDifferenceText =
            await UserHelper.GetPPDifferenceTextAsync(Context.Database, user, playmode, currentPP, savedPPInDatabase);

        var textToSend = language.command_user.Fill([
            $"{playmode.ToGamemode()}",
            $"{UserHelper.GetUserProfileUrlWrappedInUsernameString(user.Id.Value, user.Username!)}",
            $"{UserHelper.GetUserRankText(user.Statistics.GlobalRank)}",
            $"{UserHelper.GetUserRankText(user.Statistics.CountryRank)}",
            $"{user.CountryCode}",
            $"{ScoreHelper.GetScorePPText(currentPP)}",
            $"{ppDifferenceText}",
            $"{user.Statistics.HitAccuracy:N2}%",
            $"{user.Statistics.PlayCount:N0}",
            $"{user.Statistics.PlayTime / 3600}",
            $"{user.Statistics.GradeCounts!.SSH}",
            $"{user.Statistics.GradeCounts!.SH}",
            $"{user.Statistics.GradeCounts!.SS}",
            $"{user.Statistics.GradeCounts!.S}",
            $"{user.Statistics.GradeCounts!.A}"
        ]);
        var ik = new InlineKeyboardMarkup(new InlineKeyboardButton[][]
        {
            [
                new InlineKeyboardButton("Standard")
                    { CallbackData = $"{Context.Update.Chat.Id} user 0 {user.Username}" },
                new InlineKeyboardButton("Taiko") { CallbackData = $"{Context.Update.Chat.Id} user 1 {user.Username}" }
            ],
            [
                new InlineKeyboardButton("Catch") { CallbackData = $"{Context.Update.Chat.Id} user 2 {user.Username}" },
                new InlineKeyboardButton("Mania") { CallbackData = $"{Context.Update.Chat.Id} user 3 {user.Username}" }
            ]
        });

        await waitMessage.EditAsync(Context.BotClient, textToSend, replyMarkup: ik);
    }
}