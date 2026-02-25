using Microsoft.Extensions.DependencyInjection;
using OsuApi.BanchoV2;
using OsuApi.BanchoV2.Clients.Users.HttpIO;
using SosuBot.Database;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.Helpers;
using SosuBot.TelegramHandlers.Abstract;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;

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
        var language = Context.GetLocalization();

        var parameters = Context.Update.Data!.Split(' ');
        var playmode = (Playmode)int.Parse(parameters[1]);
        var osuUsername = string.Join(' ', parameters[2..]);

        var chatId = Context.Update.Message!.Chat.Id;

        var user = (await _osuApiV2.Users.GetUser($"@{osuUsername}", new GetUserQueryParameters(),
            playmode.ToRuleset()))!.UserExtend!;

        double? currentPp = user.Statistics!.Pp;
        var ppDifferenceText = await UserHelper.GetPpDifferenceTextAsync(_database, user, playmode, currentPp);

        var textToSend = LocalizationMessageHelper.UserProfileText(
            language,
            _scoreHelper,
            user,
            playmode,
            currentPp,
            ppDifferenceText,
            $"{OsuConstants.TotalAchievementsCount}");

        var ik = UserHelper.BuildUserModeKeyboard(user.Username!);

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



