using OsuApi.V2.Users.Models;
using SosuBot.Extensions;
using SosuBot.Helpers;
using SosuBot.Helpers.OutputText;
using SosuBot.Helpers.Scoring;
using SosuBot.Helpers.Types.Statistics;
using SosuBot.Localization;
using SosuBot.Localization.Languages;
using SosuBot.Services.BackgroundServices;
using SosuBot.Services.Handlers.Abstract;
using Telegram.Bot.Types;

namespace SosuBot.Services.Handlers.Commands
{
    public class GetRankingCommand : CommandBase<Message>
    {
        public static string[] Commands = ["/ranking"];

        public override async Task ExecuteAsync()
        {
            if (await Context.Update.IsUserSpamming(Context.BotClient))
                return;

            ILocalization language = new Russian();
            Message waitMessage = await Context.Update.ReplyAsync(Context.BotClient, language.waiting);

            string[] parameters = Context.Update.Text!.GetCommandParameters()!;

            string? countryCode = parameters.Length > 0 ? parameters[0] : null;
            List<UserStatistics> users = await OsuApiHelper.GetUsersFromRanking(Context.OsuApiV2, countryCode, 50);

            string rankingText = "";
            for (int i = 0; i < users.Count; i++)
            {
                rankingText += $"{i+1}. {UserHelper.GetUserProfileUrlWrappedInUsernameString(users[i].User!.Id!.Value, users[i].User!.Username!)}\n";
            }

            string sendText = $"Топ игроков в <b>{countryCode?.ToUpperInvariant() ?? "global"}</b>:\n\n" +
                              rankingText;

            await waitMessage.EditAsync(Context.BotClient, sendText);
        }
    }
}