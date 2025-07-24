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
            string sendText = await ScoreHelper.GetDailyStatisticsSendText(
                ScoresObserverBackgroundService.AllDailyStatistics.Last(), Context.OsuApiV2, Context.Logger);
            await Context.Update.ReplyAsync(Context.BotClient, sendText);
        }
    }
}
