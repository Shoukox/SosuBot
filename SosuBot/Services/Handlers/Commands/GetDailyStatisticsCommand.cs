using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using osu.Game.Configuration;
using OsuApi.V2;
using OsuApi.V2.Models;
using SosuBot.Extensions;
using SosuBot.Helpers.OutputText;
using SosuBot.Localization;
using SosuBot.Localization.Languages;
using SosuBot.Services.Handlers.Abstract;
using SosuBot.Services.Synchronization;
using Telegram.Bot.Types;

namespace SosuBot.Services.Handlers.Commands;

public sealed class GetDailyStatisticsCommand : CommandBase<Message>
{
    public static readonly string[] Commands = ["/get", "/daily_stats"];
    private ApiV2 _osuApiV2 = null!;
    private ILogger<GetDailyStatisticsCommand> _logger = null!;
    private ScoreHelper _scoreHelper = null!;
    private RateLimiterFactory _rateLimiterFactory = null!;

    public override Task BeforeExecuteAsync()
    {
        _osuApiV2 = Context.ServiceProvider.GetRequiredService<ApiV2>();
        _scoreHelper = Context.ServiceProvider.GetRequiredService<ScoreHelper>();
        _rateLimiterFactory = Context.ServiceProvider.GetRequiredService<RateLimiterFactory>();
        _logger = Context.ServiceProvider.GetRequiredService<ILogger<GetDailyStatisticsCommand>>();
        return Task.CompletedTask;
    }

    public override async Task ExecuteAsync()
    {
        await BeforeExecuteAsync();

        var rateLimiter = _rateLimiterFactory.Get(RateLimiterFactory.RateLimitPolicy.Command);
        if (!await rateLimiter.IsAllowedAsync($"{Context.Update.From!.Id}"))
        {
            await Context.Update.ReplyAsync(Context.BotClient, "Давай не так быстро!");
            return;
        }

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
            (Context.Database.DailyStatistics.OrderBy(m => m.Id).Last() is var dailyStats &&
             (dailyStats.Scores.Count == 0 || dailyStats.ActiveUsers.Count == 0)))
        {
            await waitMessage.EditAsync(Context.BotClient, language.error_noRecords);
            return;
        }

        var playmode = ruleset.ParseRulesetToPlaymode();

        sendText = await _scoreHelper.GetDailyStatisticsSendText(playmode, dailyStats, _osuApiV2);

        await waitMessage.EditAsync(Context.BotClient, sendText);
    }
}