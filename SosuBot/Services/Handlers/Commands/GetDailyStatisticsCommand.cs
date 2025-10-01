using SosuBot.Extensions;
using SosuBot.Helpers.OutputText;
using SosuBot.Localization;
using SosuBot.Localization.Languages;
using SosuBot.Services.BackgroundServices;
using SosuBot.Services.Handlers.Abstract;
using Telegram.Bot.Types;

namespace SosuBot.Services.Handlers.Commands;

public class GetDailyStatisticsCommand : CommandBase<Message>
{
    public static string[] Commands = ["/get", "/daily_stats"];

    public override async Task ExecuteAsync()
    {
        if (await Context.Update.IsUserSpamming(Context.BotClient))
            return;

        ILocalization language = new Russian();
        var waitMessage = await Context.Update.ReplyAsync(Context.BotClient, language.waiting);

        // Fake 500ms wait
        await Task.Delay(500);

        var parameters = Context.Update.Text!.GetCommandParameters()!;
        var sendText = "";
        if (parameters.Length == 0)
        {
            if (ScoresObserverBackgroundService.AllDailyStatistics.Count == 0 ||
                ScoresObserverBackgroundService.AllDailyStatistics.Last() is var dailyStatistics &&
                (dailyStatistics.Scores.Count == 0 || dailyStatistics.ActiveUsers.Count == 0))
            {
                await waitMessage.EditAsync(Context.BotClient, language.error_noRecords);
                return;
            }

            sendText = await ScoreHelper.GetDailyStatisticsSendText(
                dailyStatistics, Context.OsuApiV2);
        }
        else if (parameters[0] == "online")
        {
            var countryCode = "uz";
            if (parameters.Length > 1 && parameters[1].Length == 2)
            {
                await waitMessage.EditAsync(Context.BotClient,
                    $"Бот не отлеживает страну с кодом <b>{parameters[1]}</b>");
                return;
            }

            var ranking =
                ScoresObserverBackgroundService.ActualCountryRankings.FirstOrDefault(m =>
                    m.CountryCode == countryCode);
            if (ranking == null)
            {
                await waitMessage.EditAsync(Context.BotClient,
                    "Подожди некоторое время... отслеживание в процессе.\n\nЕсли ты ждешь больше 10 минут, то скорее всего случилась ошибка.");
                return;
            }

            var onlineUsers = ranking.Ranking.Where(m => m.User!.IsOnline!.Value).ToArray();
            var onlineUsersText =
                string.Join(" ",
                    onlineUsers
                        .Select(m =>
                            UserHelper.GetUserProfileUrlWrappedInUsernameString(m.User!.Id!.Value, m.User.Username!))
                );

            DateTime tashkentNow = TimeZoneInfo.ConvertTime(ranking.StatisticFrom, TimeZoneInfo.FindSystemTimeZoneById("West Asia Standard Time"));
            sendText = $"Онлайн пользователи (osu!lazer + osu! website) из <b>{countryCode.ToUpperInvariant()}</b>.\n" +
                       $"Последнее отслеживание: <b>{tashkentNow:dd.MM.yyyy HH:mm} (по тшк.)</b>\n" +
                       $"Количество: <b>{onlineUsers.Length}</b>\n\n" +
                       $"{onlineUsersText}";
        }
        else
        {
            sendText = language.error_argsLength;
        }

        await waitMessage.EditAsync(Context.BotClient, sendText);
    }
}