using Microsoft.Extensions.DependencyInjection;
using OsuApi.V2;
using OsuApi.V2.Clients.Users.HttpIO;
using SosuBot.Extensions;
using SosuBot.Helpers;
using SosuBot.Helpers.OutputText;
using SosuBot.Helpers.Types;
using SosuBot.Localization;
using SosuBot.Localization.Languages;
using SosuBot.Services.Handlers.Abstract;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace SosuBot.Services.Handlers.Callbacks;

public class OsuUserCallbackCommand : CommandBase<CallbackQuery>
{
    public static readonly string Command = "user";
    private ApiV2 _osuApiV2 = null!;

    public override Task BeforeExecuteAsync()
    {
        _osuApiV2 = Context.ServiceProvider.GetRequiredService<ApiV2>();
        return Task.CompletedTask;
    }

    public override async Task ExecuteAsync()
    {
        await BeforeExecuteAsync();

        ILocalization language = new Russian();

        var parameters = Context.Update.Data!.Split(' ');
        var chatId = long.Parse(parameters[0]);
        var playmode = (Playmode)int.Parse(parameters[2]);
        var osuUsername = string.Join(' ', parameters[3..]);

        var user = (await _osuApiV2.Users.GetUser($"@{osuUsername}", new GetUserQueryParameters(),
            playmode.ToRuleset()))!.UserExtend!;

        double? currentPp = user.Statistics!.Pp;
        var ppDifferenceText =
            await UserHelper.GetPpDifferenceTextAsync(Context.Database, user, playmode, currentPp);

        // should be equal to the variant from OsuUserCommand
        DateTime.TryParse(user.JoinDate?.Value, out var registerDateTime);
        var textToSend = language.command_user.Fill([
            $"{playmode.ToGamemode()}",
            $"{UserHelper.GetUserProfileUrlWrappedInUsernameString(user.Id.Value, user.Username!)}",
            $"{UserHelper.GetUserRankText(user.Statistics.GlobalRank)}",
            $"{UserHelper.GetUserRankText(user.Statistics.CountryRank)}",
            $"{UserHelper.CountryCodeToFlag(user.CountryCode ?? "nn")}",
            $"{ScoreHelper.GetFormattedPpTextConsideringNull(currentPp)}",
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
                new InlineKeyboardButton("Standard") { CallbackData = $"{chatId} user 0 {user.Username}" },
                new InlineKeyboardButton("Taiko") { CallbackData = $"{chatId} user 1 {user.Username}" }
            ],
            [
                new InlineKeyboardButton("Catch") { CallbackData = $"{chatId} user 2 {user.Username}" },
                new InlineKeyboardButton("Mania") { CallbackData = $"{chatId} user 3 {user.Username}" }
            ]
        });

        try
        {
            await Context.Update.Message!.EditAsync(Context.BotClient, textToSend, replyMarkup: ik);
        }
        catch (ApiRequestException e) when (e.ErrorCode == 400)
        {
            await Context.Update.AnswerAsync(Context.BotClient);
        }
    }
}