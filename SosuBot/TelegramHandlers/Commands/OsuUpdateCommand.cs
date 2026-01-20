using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OsuApi.BanchoV2;
using OsuApi.BanchoV2.Models;
using OsuApi.BanchoV2.Users.Models;
using SosuBot.Database;
using SosuBot.Extensions;
using SosuBot.Helpers;
using SosuBot.Helpers.OutputText;
using SosuBot.Localization;
using SosuBot.Localization.Languages;
using SosuBot.Services.BackgroundServices;
using SosuBot.Services.Synchronization;
using SosuBot.TelegramHandlers.Abstract;
using Telegram.Bot.Types;

namespace SosuBot.TelegramHandlers.Commands;

public sealed class OsuUpdateCommand : CommandBase<Message>
{
    public static readonly string[] Commands = ["/update", "/upd", "/up", "/info"];
    private BanchoApiV2 _osuApiV2 = null!;
    private RateLimiterFactory _rateLimiterFactory = null!;
    private ScoreHelper _scoreHelper = null!;
    private BotContext _database = null!;

    public override async Task BeforeExecuteAsync()
    {
        await base.BeforeExecuteAsync();
        _osuApiV2 = Context.ServiceProvider.GetRequiredService<BanchoApiV2>();
        _rateLimiterFactory = Context.ServiceProvider.GetRequiredService<RateLimiterFactory>();
        _scoreHelper = Context.ServiceProvider.GetRequiredService<ScoreHelper>();
        _database = Context.ServiceProvider.GetRequiredService<BotContext>();
    }
    public override async Task ExecuteAsync()
    {
        var rateLimiter = _rateLimiterFactory.Get(RateLimiterFactory.RateLimitPolicy.UpdateCommand);
        if (!await rateLimiter.IsAllowedAsync($"{Context.Update.From!.Id}"))
        {
            await Context.Update.ReplyAsync(Context.BotClient, "Эту команду можно применять макс. 5 раз за 24 часа. Подожди немного.");
            return;
        }

        ILocalization language = new Russian();
        var waitMessage = await Context.Update.ReplyAsync(Context.BotClient, language.waiting);

        var osuUserInDatabase = await _database.OsuUsers.FindAsync(Context.Update.From!.Id);
        if (osuUserInDatabase is null)
        {
            await waitMessage.EditAsync(Context.BotClient, language.error_userNotSetHimself);
            return;
        }


        // Fake 500ms wait
        await Task.Delay(500);

        var userScores = _database.ScoreEntity.Where(m => m.ScoreJson.UserId == osuUserInDatabase!.OsuUserId).ToList();

        bool uzbekPlayer = userScores.Count > 0;

        string sendMessage = $"Последняя информация о <b>{UserHelper.GetUserProfileUrlWrappedInUsernameString((int)osuUserInDatabase.OsuUserId, osuUserInDatabase.OsuUsername)}</b>\n\n";

        if (uzbekPlayer)
        {
            var newestScore = userScores.MaxBy(m => m.ScoreJson.EndedAt);
            if (newestScore?.ScoreJson.EndedAt != null)
            {
                sendMessage += $"- Твой последний скор был сделан {newestScore.ScoreJson.EndedAt:dd.MM.yyyy HH:mm:ss} - <a href=\"{OsuConstants.BaseScoreUrl}{newestScore.ScoreId}\">ссылка на скор</a>\n";
            }

            var monthUserScores = userScores.Where(m => m.ScoreJson.EndedAt > DateTime.UtcNow.Date.AddDays(-DateTime.UtcNow.Date.Day + 1)).ToArray();
            sendMessage += $"- За этот месяц ты поставил {monthUserScores.Length} скоров\n";
        }

        string playerRuleset = osuUserInDatabase.OsuMode.ToRuleset();
        var userBestScores = await _osuApiV2.Users.GetUserScores(osuUserInDatabase.OsuUserId, ScoreType.Best, new() { Limit = 200, Mode = playerRuleset });
        var timeSortedUserBestScores = userBestScores!.Scores.Where(m => m.EndedAt > DateTime.UtcNow.Date.AddDays(-DateTime.UtcNow.Date.Day + 1)).ToArray();
        sendMessage += $"- За этот месяц ты поставил {timeSortedUserBestScores.Length} новых топ плеев (<i>{playerRuleset.ParseRulesetToGamemode()}</i>)\n";
        sendMessage += $"- Подробнее: https://ameobea.me/osutrack/user/{osuUserInDatabase!.OsuUsername}\n\n";

        var lastBestScores = userBestScores!.Scores.OrderByDescending(m => m.EndedAt).ToArray();
        int newTopPlays = Math.Min(5, lastBestScores.Length);
        if (newTopPlays > 0)
        {
            sendMessage += $"{newTopPlays} твоих последних новых топ скоров:\n";
            for (int i = 0; i < newTopPlays; i++)
            {
                sendMessage += $"#{userBestScores.Scores.IndexOf(lastBestScores[i]) + 1} - " +
                    $"{_scoreHelper.GetScoreUrlWrappedInString(osuUserInDatabase.OsuUserId, $"{_scoreHelper.GetFormattedNumConsideringNull(lastBestScores[i].Pp, format: "N0")}pp")} - " +
                    $"{lastBestScores[i].EndedAt:dd.MM.yyyy HH:mm:ss}\n";
            }
        }

        await waitMessage.EditAsync(Context.BotClient, sendMessage);
    }
}