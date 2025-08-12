using SosuBot.Extensions;
using SosuBot.Helpers;
using SosuBot.Helpers.OutputText;
using SosuBot.Localization;
using SosuBot.Localization.Languages;
using SosuBot.Services.Handlers.Abstract;
using Telegram.Bot.Types;

namespace SosuBot.Services.Handlers.Commands;

public class GetRankingCommand : CommandBase<Message>
{
    public static string[] Commands = ["/ranking"];

    public override async Task ExecuteAsync()
    {
        if (await Context.Update.IsUserSpamming(Context.BotClient))
            return;

        ILocalization language = new Russian();
        var waitMessage = await Context.Update.ReplyAsync(Context.BotClient, language.waiting);

        var parameters = Context.Update.Text!.GetCommandParameters()!;

        var countryCode = parameters.Length > 0 ? parameters[0] : null;
        var users = await OsuApiHelper.GetUsersFromRanking(Context.OsuApiV2, countryCode, 20,
            Context.CancellationToken);

        if (users == null)
        {
            await waitMessage.EditAsync(Context.BotClient, language.error_noRankings);
            return;
        }

        var rankingText = "";
        for (var i = 0; i < users.Count; i++)
            rankingText +=
                $"{i + 1}. {UserHelper.GetUserProfileUrlWrappedInUsernameString(users[i].User!.Id!.Value, users[i].User!.Username!)} - <b>{users[i].Pp:N2}pp</b>\n";

        var sendText = $"Топ игроков в <b>{countryCode?.ToUpperInvariant() ?? "global"}</b>:\n\n" +
                       rankingText;

        await waitMessage.EditAsync(Context.BotClient, sendText);
    }
}