using SosuBot.Extensions;
using SosuBot.Helpers.OutputText;
using SosuBot.Helpers.Scoring;
using SosuBot.Localization;
using SosuBot.Localization.Languages;
using SosuBot.Services.BackgroundServices;
using SosuBot.Services.Handlers.Abstract;
using Telegram.Bot.Types;

namespace SosuBot.Services.Handlers.Commands
{
    public class GetDailyStatisticsCommand : CommandBase<Message>
    {
        public static string[] Commands = ["/get"];
        public override async Task ExecuteAsync()
        {
            if(await Context.Update.IsUserSpamming(Context.BotClient))
                return;

            if (ScoresObserverBackgroundService.AllDailyStatistics.Count == 0) return;
            
            ILocalization language = new Russian();
            Message waitMessage = await Context.Update.ReplyAsync(Context.BotClient, language.waiting);

            if (ScoresObserverBackgroundService.AllDailyStatistics.Last() is var dailyStatistics &&
                (dailyStatistics.Scores.Count == 0 || dailyStatistics.ActiveUsers.Count == 0))
            {
                await waitMessage.EditAsync(Context.BotClient, language.error_noRecords);
                return;
            }
            
            string sendText = await ScoreHelper.GetDailyStatisticsSendText(
                dailyStatistics, Context.OsuApiV2, Context.Logger);
            await waitMessage.EditAsync(Context.BotClient, sendText);
        }
    }
}
