using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OsuApi.V2;
using OsuApi.V2.Clients.Users.HttpIO;
using OsuApi.V2.Models;
using OsuApi.V2.Users.Models;
using SosuBot.Database;
using SosuBot.Helpers.Comparers;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace SosuBot.Services.BackgroundServices;

public sealed class ScoresObserverBackgroundService(
    ApiV2 osuApi,
    ILogger<ScoresObserverBackgroundService> logger,
    ITelegramBotClient botClient,
    BotContext database) : BackgroundService
{
    public static readonly ConcurrentBag<long> ObservedUsers = new();
    public const string BaseOsuScoreLink = "https://osu.ppy.sh/scores/";
    private static ScoreEqualityComparer scoreComparer = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Scores observed background service started");

        await AddPlayersToObserverList("uz", 50);
        await AddPlayersToObserverList(null, 50);

        await ObserveScores(stoppingToken);
    }

    private async Task ObserveScores(CancellationToken stoppingToken)
    {
        Dictionary<int, GetUserScoresResponse> scores = new();
        while (!stoppingToken.IsCancellationRequested)
        {
            long adminTelegramId =
                (await database.OsuUsers.FirstAsync((m) => m.IsAdmin, cancellationToken: stoppingToken)).TelegramId;
            foreach (int userId in ObservedUsers)
            {
                GetUserScoresResponse? userBestScores =
                    await osuApi.Users.GetUserScores(userId, ScoreType.Best, new() { Limit = 50 });
                if (userBestScores == null)
                {
                    logger.LogWarning($"{userId} has no scores!");
                    continue;
                }
                
                // ReSharper disable once CanSimplifyDictionaryLookupWithTryGetValue
                if (scores.ContainsKey(userId))
                {
                    IEnumerable<Score> newScores = userBestScores.Scores.Except(scores[userId].Scores, scoreComparer);
                    foreach (Score score in newScores)
                    {
                        await botClient.SendMessage(adminTelegramId,
                            $"{score.User?.Username} set a {score.Pp}pp <a href=\"{BaseOsuScoreLink}{score.Id}\">score!</a>",
                            ParseMode.Html, cancellationToken: stoppingToken);
                        await Task.Delay(1000, stoppingToken);
                    }
                }

                scores[userId] = userBestScores;
                await Task.Delay(1000, stoppingToken);
            }
        }
    }

    /// <summary>
    /// Get best players using some filter
    /// </summary>
    /// <param name="countryCode">If null, take from the global ranking</param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    private async Task<UserStatistics[]> GetBestPlayersFor(string? countryCode = null)
    {
        Rankings? rankings = await osuApi.Rankings.GetRanking(Ruleset.Osu, RankingType.Performance,
            new() { Country = countryCode, Filter = Filter.All});
        if (rankings == null)
        {
            logger.LogError($"Ranking is null. {countryCode}");
            throw new Exception("Ranking is null. See logs for details");
        }

        return rankings.Ranking!;
    }

    /// <summary>
    /// Add players to the <see cref="ObservedUsers"/>
    /// </summary>
    /// <param name="countryCode">If null, take from the global ranking</param>
    /// <param name="count">Amount of players to add</param>
    private async Task AddPlayersToObserverList(string? countryCode = null, int count = 50)
    {
        UserStatistics[] bestPlayersStatistics = (await GetBestPlayersFor(countryCode)).Take(count).ToArray();
        foreach (UserStatistics playerStatistics in bestPlayersStatistics)
        {
            ObservedUsers.Add(playerStatistics.User!.Id.Value);
        }
    }
}