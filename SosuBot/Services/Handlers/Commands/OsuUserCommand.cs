using OsuApi.Core.V2.Users.Models;
using Sosu.Localization;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.Helpers;
using SosuBot.OsuTypes;
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
            OsuUser? osuUserInDatabase = await Database.OsuUsers.FindAsync(Context.From!.Id);
            string[] parameters = Context.Text!.GetCommandParameters()!;

            Message waitMessage = await Context.ReplyAsync(BotClient, language.waiting);

            UserExtend user;
            Playmode playmode = Playmode.Osu;

            if (parameters.Length == 0)
            {
                if (osuUserInDatabase is null)
                {
                    await waitMessage.EditAsync(BotClient, language.error_userNotSetHimself);
                    return;
                }

                playmode = osuUserInDatabase.OsuMode;
                user = (await OsuApiV2.Users.GetUser($"@{osuUserInDatabase.OsuUsername}", new(), playmode.ToRuleset()))!.UserExtend!;
            }
            else if (parameters.Length == 1)
            {
                if (parameters[0].StartsWith("mode="))
                {
                    if (osuUserInDatabase is null)
                    {
                        await waitMessage.EditAsync(BotClient, language.error_userNotSetHimself);
                        return;
                    }

                    string? ruleset = parameters[0].ParseToRuleset();
                    if (ruleset is null)
                    {
                        await waitMessage.EditAsync(BotClient, language.error_modeIncorrect);
                        return;
                    }
                    playmode = ruleset.ParseRulesetToPlaymode();

                    var userResponse = await OsuApiV2.Users.GetUser($"@{osuUserInDatabase.OsuUsername}", new(), ruleset);
                    if (userResponse is null)
                    {
                        await waitMessage.EditAsync(BotClient, language.error_userNotFound);
                        return;
                    }
                    user = userResponse.UserExtend!;
                }
                else
                {
                    var userResponse = await OsuApiV2.Users.GetUser($"@{parameters[0]}", new());
                    if (userResponse is null)
                    {
                        await waitMessage.EditAsync(BotClient, language.error_userNotFound);
                        return;
                    }
                    user = userResponse.UserExtend!;

                    playmode = user.Playmode!.ParseRulesetToPlaymode();
                }
            }
            else
            {
                await waitMessage.EditAsync(BotClient, language.error_argsLength);
                return;
            }
            double? savedPPInDatabase = null;
            double? currentPP = user.Statistics!.Pp;
            string ppDifferenceText = await UserHelper.GetPPDifferenceTextAsync(Database, user, playmode, currentPP, savedPPInDatabase);

            string textToSend = language.command_user.Fill([
                $"{playmode.ToGamemode()}",
                $"{user.GetProfileUrl()}",
                $"{user.Username}",
                $"{user.Statistics.GlobalRank}",
                $"{user.Statistics.CountryRank}",
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
                [new InlineKeyboardButton("Standard") {CallbackData = $"{Context.Chat.Id} user 0 {user.Username}"}, new InlineKeyboardButton("Taiko") { CallbackData = $"{Context.Chat.Id} user 1 {user.Username}" }],
                [new InlineKeyboardButton("Catch") {CallbackData = $"{Context.Chat.Id} user 2 {user.Username}" }, new InlineKeyboardButton("Mania") { CallbackData = $"{Context.Chat.Id} user 3 {user.Username}" }]
            });

            await waitMessage.EditAsync(BotClient, textToSend, replyMarkup: ik);
        }
    }
}
