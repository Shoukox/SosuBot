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
            
            string sendText = await ScoreHelper.GetDailyStatisticsSendText(
                ScoresObserverBackgroundService.AllDailyStatistics.Last(), Context.OsuApiV2, Context.Logger);
            await waitMessage.EditAsync(Context.BotClient, sendText);
        }
    }
}
