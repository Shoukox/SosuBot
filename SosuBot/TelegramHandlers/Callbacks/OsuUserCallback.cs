using Microsoft.Extensions.DependencyInjection;
using OsuApi.BanchoV2;
using OsuApi.BanchoV2.Clients.Users.HttpIO;
using SosuBot.Database;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.Helpers;
using SosuBot.Helpers.OutputText;
using SosuBot.Localization;
using SosuBot.Localization.Languages;
using SosuBot.TelegramHandlers.Abstract;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace SosuBot.TelegramHandlers.Callbacks;

public class OsuUserCallback : CommandBase<CallbackQuery>
{
    public static readonly string Command = "user";
    private BanchoApiV2 _osuApiV2 = null!;
    private ScoreHelper _scoreHelper = null!;
    private BotContext _database = null!;

    public override async Task BeforeExecuteAsync()
    {
        await base.BeforeExecuteAsync();
        _osuApiV2 = Context.ServiceProvider.GetRequiredService<BanchoApiV2>();
        _scoreHelper = Context.ServiceProvider.GetRequiredService<ScoreHelper>();
        _database = Context.ServiceProvider.GetRequiredService<BotContext>();
    }

    public override async Task ExecuteAsync()
    {
        ILocalization language = new Russian();

        var parameters = Context.Update.Data!.Split(' ');
        var playmode = (Playmode)int.Parse(parameters[1]);
        var osuUsername = string.Join(' ', parameters[2..]);

        var chatId = Context.Update.Message!.Chat.Id;

        var user = (await _osuApiV2.Users.GetUser($"@{osuUsername}", new GetUserQueryParameters(),
            playmode.ToRuleset()))!.UserExtend!;

        double? currentPp = user.Statistics!.Pp;
        var ppDifferenceText = await UserHelper.GetPpDifferenceTextAsync(_database, user, playmode, currentPp);

        // should be equal to the variant from OsuUserCommand
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