using Microsoft.Extensions.DependencyInjection;
using OsuApi.V2;
using OsuApi.V2.Models;
using SosuBot.Extensions;
using SosuBot.Helpers.OutputText;
using SosuBot.Helpers.Types;
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

    public override Task BeforeExecuteAsync()
    {
        _osuApiV2 = Context.ServiceProvider.GetRequiredService<ApiV2>();
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
        
        var sendText = "";
        if (parameters.Length == 0)
        {
            sendText = language.error_argsLength + "\n/daily_stats osu/catch/taiko/mania";
            await waitMessage.EditAsync(Context.BotClient, sendText);
            return;
        }

        string? ruleset = parameters[0].ParseToRuleset();
        if (string.IsNullOrEmpty(ruleset))
        {
            ruleset = Ruleset.Osu;
        }

        if (ScoresObserverBackgroundService.AllDailyStatistics.Count == 0 ||
            (ScoresObserverBackgroundService.AllDailyStatistics.Last() is var dailyStatistics &&
             (dailyStatistics.Scores.Count == 0 || dailyStatistics.ActiveUsers.Count == 0)))
        {
            await waitMessage.EditAsync(Context.BotClient, language.error_noRecords);
            return;
        }

        Playmode playmode = ruleset.ParseRulesetToPlaymode();
        sendText = await ScoreHelper.GetDailyStatisticsSendText(playmode, dailyStatistics, _osuApiV2);

        await waitMessage.EditAsync(Context.BotClient, sendText);
    }
}