using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NUnit.Framework.Internal;
using osu.Game.Rulesets.Osu.Mods;
using OsuApi.V2;
using OsuApi.V2.Clients.Users.HttpIO;
using OsuApi.V2.Models;
using OsuApi.V2.Users.Models;
using SosuBot.Extensions;
using SosuBot.Helpers;
using SosuBot.Helpers.OutputText;
using SosuBot.Helpers.Types;
using SosuBot.Localization;
using SosuBot.Localization.Languages;
using SosuBot.PerformanceCalculator;
using SosuBot.Services.BackgroundServices;
using SosuBot.Services.Handlers.Abstract;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Country = SosuBot.Helpers.Country;

namespace SosuBot.Services.Handlers.Commands;

public sealed class CustomCommand : CommandBase<Message>
{
    public static readonly string[] Commands = ["/c"];
    private OpenAiService _openaiService = null!;
    private ApiV2 _osuApiV2 = null!;
    private ILogger<CustomCommand> _logger = null!;
    private ILogger<PPCalculator> _loggerPpCalculator = null!;

    public override Task BeforeExecuteAsync()
    {
        _openaiService = Context.ServiceProvider.GetRequiredService<OpenAiService>();
        _osuApiV2 = Context.ServiceProvider.GetRequiredService<ApiV2>();
        _logger = Context.ServiceProvider.GetRequiredService<ILogger<CustomCommand>>();
        _loggerPpCalculator = Context.ServiceProvider.GetRequiredService<ILogger<PPCalculator>>();
        return Task.CompletedTask;
    }

    public override async Task ExecuteAsync()
    {
        await BeforeExecuteAsync();

        var osuUserInDatabase = await Context.Database.OsuUsers.FindAsync(Context.Update.From!.Id);
        if (osuUserInDatabase is null || !osuUserInDatabase.IsAdmin)
        {
            await Context.Update.ReplyAsync(Context.BotClient, "Пшол вон!");
            return;
        }

        var parameters = Context.Update.Text!.GetCommandParameters()!;
        if (parameters[0] == "json")
        {
            var result = JsonConvert.SerializeObject(Context.Update,
                Formatting.Indented,
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

            if (parameters.Length >= 2 && parameters[1] == "text")
                await Context.Update.ReplyAsync(Context.BotClient, result);
            else
                await Context.Update.ReplyDocumentAsync(Context.BotClient, TextHelper.TextToStream(result));
        }
        else if (parameters[0] == "test")
        {
            await Context.Update.ReplyAsync(Context.BotClient, new string('a', (int)Math.Pow(2, 14)));
        }
        else if (parameters[0] == "getuser")
        {
            var osuUserInReply = await Context.Database.OsuUsers.FindAsync(Context.Update.ReplyToMessage!.From!.Id);

            var result = JsonConvert.SerializeObject(osuUserInReply,
                Formatting.Indented,
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

            await Context.Update.ReplyAsync(Context.BotClient, result);
        }
        else if (parameters[0] == "countryflag")
        {
            await Context.Update.ReplyAsync(Context.BotClient, UserHelper.CountryCodeToFlag(parameters[1]));
        }
        else if (parameters[0] == "ai")
        {
            ILocalization language = new Russian();
            var waitMessage = await Context.Update.ReplyAsync(Context.BotClient, language.waiting);

            var userInput = string.Join(" ", parameters[1..]);
            var output = await _openaiService.GetResponseAsync(userInput, Context.Update.From.Id);
            if (!output.IsSuccess || string.IsNullOrEmpty(output.Data))
            {
                switch (output.Exception?.Code)
                {
                    case ErrorCode.Locked:
                    {
                        await waitMessage.EditAsync(Context.BotClient, "Подожди обработки предыдущего запроса!");
                        return;
                    }
                }

                await waitMessage.EditAsync(Context.BotClient, language.error_baseMessage);
                return;
            }

            try
            {
                await waitMessage.EditAsync(Context.BotClient, output.Data!, ParseMode.Markdown);
            }
            catch
            {
                await waitMessage.EditAsync(Context.BotClient, output.Data!, ParseMode.None);
            }
        }
        else if (parameters[0] == "slavik")
        {
            ILocalization language = new Russian();
            var waitMessage = await Context.Update.ReplyAsync(Context.BotClient, language.waiting);

            var countPlayersFromRanking = 100;
            var countBestScoresPerPlayer = 200;

            var uzOsuStdUsers = await OsuApiHelper.GetUsersFromRanking(_osuApiV2, count: countPlayersFromRanking);

            var getBestScoresTask = uzOsuStdUsers!.Select(m =>
                _osuApiV2.Users.GetUserScores(m.User!.Id.Value, ScoreType.Best,
                    new GetUserScoreQueryParameters
                        { Limit = countBestScoresPerPlayer, Mode = Ruleset.Osu })).ToArray();
            await Task.WhenAll(getBestScoresTask);

            var uzBestScores = getBestScoresTask.SelectMany(m => m.Result!.Scores).ToArray();

            var bestScoresByMods = uzBestScores
                .GroupBy(m => string.Join("",
                    ScoreHelper.GetModsText(m.Mods!.Where(mod =>
                        !mod.Acronym!.Equals("CL", StringComparison.InvariantCultureIgnoreCase)).ToArray())))
                .Select(m => (m.Key, m.MaxBy(s => s.Pp)!)).OrderByDescending(m => m.Item2.Pp).ToArray();

            var sendText = "";
            foreach (var pair in bestScoresByMods)
            {
                var lazer =
                    pair.Item2.Mods!.Any(m => m.Acronym!.Equals("CL", StringComparison.InvariantCultureIgnoreCase))
                        ? ""
                        : "lazer";
                sendText +=
                    $"{pair.Key} - max. {ScoreHelper.GetScoreUrlWrappedInString(pair.Item2.Id!.Value, $"{pair.Item2.Pp:N2}pp")}{lazer} by {UserHelper.GetUserProfileUrlWrappedInUsernameString(pair.Item2.UserId!.Value, pair.Item2.User!.Username!)}\n";
            }

            await waitMessage.EditAsync(Context.BotClient, sendText, splitValue: "\n");
        }
        else if (parameters[0] == "add-daily-stats-from-last")
        {
            ILocalization language = new Russian();
            var waitMessage = await Context.Update.ReplyAsync(Context.BotClient, language.waiting);

            Task<(int newUsers, int newScores, int newBeatmaps)>[] resultTasks =
            [
                ScoreHelper.UpdateDailyStatisticsFromLast(_osuApiV2, Playmode.Osu,
                    ScoresObserverBackgroundService.AllDailyStatistics.Last()),
                ScoreHelper.UpdateDailyStatisticsFromLast(_osuApiV2, Playmode.Taiko,
                    ScoresObserverBackgroundService.AllDailyStatistics.Last()),
                ScoreHelper.UpdateDailyStatisticsFromLast(_osuApiV2, Playmode.Catch,
                    ScoresObserverBackgroundService.AllDailyStatistics.Last()),
                ScoreHelper.UpdateDailyStatisticsFromLast(_osuApiV2, Playmode.Mania,
                    ScoresObserverBackgroundService.AllDailyStatistics.Last())
            ];

            await waitMessage.EditAsync(Context.BotClient,
                $"osu!std newUsers: {resultTasks[0].Result.newUsers} | newScores:{resultTasks[0].Result.newScores} | newBeatmaps:{resultTasks[0].Result.newBeatmaps}\n" +
                $"osu!taiko newUsers: {resultTasks[1].Result.newUsers} | newScores:{resultTasks[1].Result.newScores} | newBeatmaps:{resultTasks[1].Result.newBeatmaps}\n" +
                $"osu!catch newUsers: {resultTasks[2].Result.newUsers} | newScores:{resultTasks[2].Result.newScores} | newBeatmaps:{resultTasks[2].Result.newBeatmaps}\n" +
                $"osu!mania newUsers: {resultTasks[3].Result.newUsers} | newScores:{resultTasks[3].Result.newScores} | newBeatmaps:{resultTasks[3].Result.newBeatmaps}");
        }
        else if (parameters[0] == "fix-daily-stats")
        {
            ILocalization language = new Russian();
            var waitMessage = await Context.Update.ReplyAsync(Context.BotClient, language.waiting);
            var dailyStatistics = ScoresObserverBackgroundService.AllDailyStatistics.Last();
            var passedStdScores = dailyStatistics.Scores.Where(m => m.ModeInt == (int)Playmode.Osu).ToList();
            int removed = dailyStatistics.Scores.RemoveAll(m =>
            {
                return passedStdScores.Any(std => std.Id == m.Id && std.ModeInt != m.ModeInt && m.ModeInt != 0);
            });

            var tashkentToday = DateTime.Today.ChangeTimezone(Country.Uzbekistan);
            _logger.LogInformation(tashkentToday.ToString("g"));
            removed += dailyStatistics.Scores.RemoveAll(m =>
                m.EndedAt!.Value.ChangeTimezone(Country.Uzbekistan) < tashkentToday);
            await waitMessage.EditAsync(Context.BotClient, $"Scores removed: {removed}");
        }
        else if (parameters[0] == "test1")
        {
            var ppCalculator = new PPCalculator(_loggerPpCalculator);
            for (int i = 1; i <= 1000; i++)
            {
                var calculatedPp = await ppCalculator.CalculatePpAsync(970048, 0.9889,
                    scoreMaxCombo: 1466,
                    passed: true,
                    scoreMods: [new OsuModClassic()],
                    scoreStatistics: null,
                    rulesetId: (int)Playmode.Osu,
                    cancellationToken: Context.CancellationToken);
                await Task.Delay(1000);
            }
        }
    }
}