using OsuApi.Core.V2.Users.Models;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.Helpers.OsuTypes;
using SosuBot.Helpers.OutputText;
using SosuBot.Helpers.Scoring;
using SosuBot.Localization;
using SosuBot.Localization.Languages;
using SosuBot.Services.Handlers.Abstract;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace SosuBot.Services.Handlers.Commands
{
    public class OsuUserCommand : CommandBase<Message>
    {
        public static string[] Commands = ["/user", "/u"];

        public override async Task ExecuteAsync()
        {
            ILocalization language = new Russian();
            OsuUser? osuUserInDatabase = await Context.Database.OsuUsers.FindAsync(Context.Update.From!.Id);
            string[] parameters = Context.Update.Text!.GetCommandParameters()!;

            Message waitMessage = await Context.Update.ReplyAsync(Context.BotClient, language.waiting);

            UserExtend user;
            Playmode playmode = Playmode.Osu;

            if (parameters.Length == 0)
            {
                if (osuUserInDatabase is null)
                {
                    await waitMessage.EditAsync(Context.BotClient, language.error_userNotSetHimself);
                    return;
                }

                playmode = osuUserInDatabase.OsuMode;
                user = (await Context.OsuApiV2.Users.GetUser($"@{osuUserInDatabase.OsuUsername}", new(), playmode.ToRuleset()))!.UserExtend!;
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

                    string? ruleset = parameters[0].ParseToRuleset();
                    if (ruleset is null)
                    {
                        await waitMessage.EditAsync(Context.BotClient, language.error_modeIncorrect);
                        return;
                    }
                    playmode = ruleset.ParseRulesetToPlaymode();

                    var userResponse = await Context.OsuApiV2.Users.GetUser($"@{osuUserInDatabase.OsuUsername}", new(), ruleset);
                    if (userResponse is null)
                    {
                        await waitMessage.EditAsync(Context.BotClient, language.error_userNotFound);
                        return;
                    }
                    user = userResponse.UserExtend!;
                }
                else
                {
                    var userResponse = await Context.OsuApiV2.Users.GetUser($"@{parameters[0]}", new());
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
            string ppDifferenceText = await UserHelper.GetPPDifferenceTextAsync(Context.Database, user, playmode, currentPP, savedPPInDatabase);

            string textToSend = language.command_user.Fill([
                $"{playmode.ToGamemode()}",
                $"{user.GetProfileUrl()}",
                $"{user.Username}",
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
                $"{user.Statistics.GradeCounts!.A}"]);
            var ik = new InlineKeyboardMarkup(new InlineKeyboardButton[][]
            {
                [new InlineKeyboardButton("Standard") {CallbackData = $"{Context.Update.Chat.Id} user 0 {user.Username}"}, new InlineKeyboardButton("Taiko") { CallbackData = $"{Context.Update.Chat.Id} user 1 {user.Username}" }],
                [new InlineKeyboardButton("Catch") {CallbackData = $"{Context.Update.Chat.Id} user 2 {user.Username}" }, new InlineKeyboardButton("Mania") { CallbackData = $"{Context.Update.Chat.Id} user 3 {user.Username}" }]
            });

            await waitMessage.EditAsync(Context.BotClient, textToSend, replyMarkup: ik);
        }
    }
}
