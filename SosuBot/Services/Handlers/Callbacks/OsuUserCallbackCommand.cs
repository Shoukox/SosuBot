using OsuApi.V2.Clients.Users.HttpIO;
using SosuBot.Extensions;
using SosuBot.Helpers.OutputText;
using SosuBot.Helpers.Types;
using SosuBot.Localization;
using SosuBot.Localization.Languages;
using SosuBot.Services.Handlers.Abstract;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace SosuBot.Services.Handlers.Callbacks;

public class OsuUserCallbackCommand : CommandBase<CallbackQuery>
{
    public static string Command = "user";

    public override async Task ExecuteAsync()
    {
        ILocalization language = new Russian();

        var parameters = Context.Update.Data!.Split(' ');
        var chatId = long.Parse(parameters[0]);
        var playmode = (Playmode)int.Parse(parameters[2]);
        var osuUsername = string.Join(' ', parameters[3..]);

        var user = (await Context.OsuApiV2.Users.GetUser($"@{osuUsername}", new GetUserQueryParameters(),
            playmode.ToRuleset()))!.UserExtend!;

        double? currentPp = user.Statistics!.Pp;
        var ppDifferenceText =
            await UserHelper.GetPpDifferenceTextAsync(Context.Database, user, playmode, currentPp);

        var textToSend = language.command_user.Fill([
            $"{playmode.ToGamemode()}",
            $"{UserHelper.GetUserProfileUrlWrappedInUsernameString(user.Id.Value, user.Username!)}",
            $"{UserHelper.GetUserRankText(user.Statistics.GlobalRank)}",
            $"{UserHelper.GetUserRankText(user.Statistics.CountryRank)}",
            $"{user.CountryCode}",
            $"{currentPp:N2}",
            $"{ppDifferenceText}",
            $"{user.Statistics.HitAccuracy:N2}%",
            $"{user.Statistics.PlayCount:N2}",
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
                new InlineKeyboardButton("Standard") { CallbackData = $"{chatId} user 0 {user.Username}" },
                new InlineKeyboardButton("Taiko") { CallbackData = $"{chatId} user 1 {user.Username}" }
            ],
            [
                new InlineKeyboardButton("Catch") { CallbackData = $"{chatId} user 2 {user.Username}" },
                new InlineKeyboardButton("Mania") { CallbackData = $"{chatId} user 3 {user.Username}" }
            ]
        });

        await Context.Update.Message!.EditAsync(Context.BotClient, textToSend, replyMarkup: ik);
    }
}