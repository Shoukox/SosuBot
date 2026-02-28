using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using OsuApi.BanchoV2;
using OsuApi.BanchoV2.Users.Models;
using SosuBot.Database;
using SosuBot.Extensions;
using SosuBot.Helpers;
using SosuBot.Localization;
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
    private HybridCache _cache = null!;

    public override async Task BeforeExecuteAsync()
    {
        await base.BeforeExecuteAsync();
        _osuApiV2 = Context.ServiceProvider.GetRequiredService<BanchoApiV2>();
        _rateLimiterFactory = Context.ServiceProvider.GetRequiredService<RateLimiterFactory>();
        _scoreHelper = Context.ServiceProvider.GetRequiredService<ScoreHelper>();
        _database = Context.ServiceProvider.GetRequiredService<BotContext>();
        _cache = Context.ServiceProvider.GetRequiredService<HybridCache>();
    }
    public override async Task ExecuteAsync()
    {
        var language = Context.GetLocalization();
        var waitMessage = await Context.Update.ReplyAsync(Context.BotClient, language.waiting);

        var osuUserInDatabase = await _database.OsuUsers.FindAsync(Context.Update.From!.Id);
        if (osuUserInDatabase is null)
        {
            await waitMessage.EditAsync(Context.BotClient, language.error_userNotSetHimself);
            return;
        }


        // Fake 500ms wait
        await Task.Delay(500);
        var parameters = Context.Update.Text!.GetCommandParameters()!;
        if (parameters.Length != 0)
        {
            await waitMessage.EditAsync(Context.BotClient, language.error_argsLength + $"\n{language.update_onlyInfoAllowed}");
            return;
        }

        string cacheKey = $"osuinfo:{osuUserInDatabase.OsuUserId}:{language.last_humanizerCulture}";
        if (await _cache.GetOrCreateAsync<string>(cacheKey, null!, new() { Flags = HybridCacheEntryFlags.DisableUnderlyingData }) is { } sendMessage)
        {
            await waitMessage.EditAsync(Context.BotClient, sendMessage);
            return;
        }

        var userScores = _database.ScoreEntity.Where(m => m.ScoreJson.UserId == osuUserInDatabase!.OsuUserId).ToList();
        bool uzbekPlayer = userScores.Count > 0;

        sendMessage = language.update_info_header.Fill([
                UserHelper.GetUserProfileUrlWrappedInUsernameString((int)osuUserInDatabase.OsuUserId, osuUserInDatabase.OsuUsername)
            ]) + "\n" +
            language.update_info_lastUpdate.Fill([
                DateTime.UtcNow.ChangeTimezone(Helpers.Country.Uzbekistan).ToString(),
                language.daily_stats_tashkent_time
            ]) + "\n\n";

        if (uzbekPlayer)
        {
            sendMessage += language.update_info_trackingSince.Fill([
                userScores.MinBy(m => m.ScoreJson.EndedAt)!.ScoreJson.EndedAt!.Value.ChangeTimezone(Helpers.Country.Uzbekistan).ToString("dd.MM.yyyy HH:mm:ss"),
                language.daily_stats_tashkent_time
            ]) + "\n";
            sendMessage += language.update_info_trackedScoresSince.Fill([$"{userScores.Count}"]) + "\n";

            var newestScore = userScores.MaxBy(m => m.ScoreJson.EndedAt);
            if (newestScore?.ScoreJson.EndedAt != null)
            {
                sendMessage += language.update_info_lastScoreAt.Fill([
                    $"{OsuConstants.BaseScoreUrl}{newestScore.ScoreId}",
                    newestScore.ScoreJson.EndedAt!.Value.ChangeTimezone(Helpers.Country.Uzbekistan).ToString("dd.MM.yyyy HH:mm:ss"),
                    language.daily_stats_tashkent_time
                ]) + "\n";
            }

            var monthUserScores = userScores.Where(m => m.ScoreJson.EndedAt > DateTime.UtcNow.Date.AddDays(-DateTime.UtcNow.Date.Day + 1)).ToArray();
            sendMessage += language.update_info_scoresThisMonth.Fill([$"{monthUserScores.Length}"]) + "\n";
        }

        string playerRuleset = osuUserInDatabase.OsuMode.ToRuleset();
        var userBestScores = await _osuApiV2.Users.GetUserScores(osuUserInDatabase.OsuUserId, ScoreType.Best, new() { Limit = 200, Mode = playerRuleset });
        var timeSortedUserBestScores = userBestScores!.Scores.Where(m => m.EndedAt > DateTime.UtcNow.Date.AddDays(-DateTime.UtcNow.Date.Day + 1)).ToArray();
        sendMessage += language.update_info_newTopPlaysThisMonth.Fill([$"{timeSortedUserBestScores.Length}", playerRuleset.ParseRulesetToGamemode()]) + "\n";
        sendMessage += language.update_info_detailedStats.Fill([$"https://ameobea.me/osutrack/user/{osuUserInDatabase!.OsuUsername}"]) + "\n\n";

        var lastBestScores = userBestScores!.Scores.OrderByDescending(m => m.EndedAt).ToArray();
        int newTopPlays = Math.Min(5, lastBestScores.Length);
        if (newTopPlays > 0)
        {
            sendMessage += language.update_info_lastTopScoresTitle.Fill([$"{newTopPlays}"]) + "\n";
            for (int i = 0; i < newTopPlays; i++)
            {
                sendMessage += language.update_info_lastTopScoresEntry.Fill([
                        $"{userBestScores.Scores.IndexOf(lastBestScores[i]) + 1}",
                        _scoreHelper.GetScoreUrlWrappedInString(lastBestScores[i].Id.GetValueOrDefault(0), $"{_scoreHelper.GetFormattedNumConsideringNull(lastBestScores[i].Pp, format: "N0")}pp"),
                        lastBestScores[i].EndedAt!.Value.ChangeTimezone(Helpers.Country.Uzbekistan).ToString("dd.MM.yyyy HH:mm:ss"),
                        language.daily_stats_tashkent_time
                    ]) + "\n";
            }
        }

        await _cache.SetAsync(cacheKey, sendMessage, new() { Expiration = TimeSpan.FromHours(2) });

        await waitMessage.EditAsync(Context.BotClient, sendMessage);
    }
}

