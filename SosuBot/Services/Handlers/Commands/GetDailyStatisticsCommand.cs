using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OsuApi.V2;
using OsuApi.V2.Models;
using SosuBot.Extensions;
using SosuBot.Helpers.OutputText;
using SosuBot.Helpers.Types.Statistics;
using SosuBot.Localization;
using SosuBot.Localization.Languages;
using SosuBot.Services.BackgroundServices;
using SosuBot.Services.Handlers.Abstract;
using Telegram.Bot.Types;

namespace SosuBot.Services.Handlers.Commands;

public sealed class GetDailyStatisticsCommand : CommandBase<Message>
{
    public static readonly string[] Commands = ["/get", "/daily_stats"];
    private ApiV2 _osuApiV2 = null!;
    private ILogger<GetDailyStatisticsCommand> _logger = null!;

    public override Task BeforeExecuteAsync()
    {
        _osuApiV2 = Context.ServiceProvider.GetRequiredService<ApiV2>();
        _logger = Context.ServiceProvider.GetRequiredService<ILogger<GetDailyStatisticsCommand>>();
        return Task.CompletedTask;
    }

    public override async Task ExecuteAsync()
    {
        await BeforeExecuteAsync();

        if (await Context.Update.IsUserSpamming(Context.BotClient))
            return;

        ILocalization language = new Russian();
        var waitMessage = await Context.Update.ReplyAsync(Context.BotClient, language.waiting);

        // Fake 500ms wait
        await Task.Delay(500);

        var parameters = Context.Update.Text!.GetCommandParameters()!;

        string sendText;
        if (parameters.Length == 0)
        {
            sendText = language.error_argsLength + "\n/daily_stats osu/catch/taiko/mania";
            await waitMessage.EditAsync(Context.BotClient, sendText);
            return;
        }

        string? ruleset = TextHelper.GetPlaymodeFromParameters(parameters, out parameters)?.ToRuleset();
        ruleset ??= parameters[0].ParseToRuleset();
        if (string.IsNullOrEmpty(ruleset)) ruleset = Ruleset.Osu;

        if (Context.Database.DailyStatistics.Count() == 0 ||
            (Context.Database.DailyStatistics.OrderBy(m => m.Id).Last() is var lastDbDailyStats &&
             (lastDbDailyStats.Scores.Count == 0 || lastDbDailyStats.ActiveUsers.Count == 0)))
        {
            await waitMessage.EditAsync(Context.BotClient, language.error_noRecords);
            return;
        }

        var playmode = ruleset.ParseRulesetToPlaymode();

        var dailyStats = new DailyStatistics(lastDbDailyStats.CountryCode, lastDbDailyStats.DayOfStatistic)
        {
            ActiveUsers = lastDbDailyStats.ActiveUsers.Select(m => m.UserJson).ToList(),
            BeatmapsPlayed = lastDbDailyStats.BeatmapsPlayed,
            Scores = lastDbDailyStats.Scores.Select(m => m.ScoreJson).ToList(),
        };
        sendText = await ScoreHelper.GetDailyStatisticsSendText(playmode, dailyStats, _osuApiV2, Context.Redis, _logger);

        await waitMessage.EditAsync(Context.BotClient, sendText);
    }
}