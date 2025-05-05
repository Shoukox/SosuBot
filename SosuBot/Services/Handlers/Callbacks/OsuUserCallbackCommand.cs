using Microsoft.EntityFrameworkCore;
using OsuApi.Core.V2.Users.Models;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.Helpers.OsuTypes;
using SosuBot.Helpers.OutputText;
using SosuBot.Localization;
using SosuBot.Services.Handlers.Abstract;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace SosuBot.Services.Handlers.Callbacks
{
    public class OsuUserCallbackCommand : CommandBase<CallbackQuery>
    {
        public static string Command = "user";

        public override async Task ExecuteAsync()
        {
            ILocalization language = new Russian();

            string[] parameters = Context.Update.Data!.Split(' ');
            long chatId = long.Parse(parameters[0]);
            Playmode playmode = (Playmode)int.Parse(parameters[2]);
            string osuUsername = string.Join(' ', parameters[3..]);

            UserExtend user = (await Context.OsuApiV2.Users.GetUser($"@{osuUsername}", new(), playmode.ToRuleset()))!.UserExtend!;

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
                $"{currentPP:N2}",
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
                [new InlineKeyboardButton("Standard") {CallbackData = $"{chatId} user 0 {user.Username}"}, new InlineKeyboardButton("Taiko") { CallbackData = $"{chatId} user 1 {user.Username}" }],
                [new InlineKeyboardButton("Catch") {CallbackData = $"{chatId} user 2 {user.Username}" }, new InlineKeyboardButton("Mania") { CallbackData = $"{chatId} user 3 {user.Username}" }]
            });

            await Context.Update.Message!.EditAsync(Context.BotClient, textToSend, replyMarkup: ik);
        }
    }
}
