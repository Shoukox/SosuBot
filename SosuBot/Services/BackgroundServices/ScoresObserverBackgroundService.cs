using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using osu.Game.Beatmaps;
using OsuApi.V2;
using OsuApi.V2.Clients.Beatmaps.HttpIO;
using OsuApi.V2.Clients.Scores.HttpIO;
using OsuApi.V2.Clients.Users.HttpIO;
using OsuApi.V2.Models;
using OsuApi.V2.Users.Models;
using SosuBot.Database;
using SosuBot.Extensions;
using SosuBot.Helpers.Comparers;
using SosuBot.Helpers.Scoring;
using SosuBot.Helpers.Types;
using SosuBot.Helpers.Types.Statistics;
using SosuBot.Localization;
using SosuBot.Localization.Languages;
using SosuBot.Services.Data.OsuApi;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Beatmap = OsuApi.V2.Users.Models.Beatmap;

namespace SosuBot.Services.BackgroundServices;

public sealed class ScoresObserverBackgroundService(
    ApiV2 osuApi,
    ILogger<ScoresObserverBackgroundService> logger,
    ITelegramBotClient botClient,
    BotContext database) : BackgroundService
{
    public const string BaseOsuScoreLink = "https://osu.ppy.sh/scores/";
    public static readonly ConcurrentBag<long> ObservedUsers = new();
    public static List<DailyStatistics> AllDailyStatistics = new(); 
    
    private static readonly ScoreEqualityComparer ScoreComparer = new();
    
    private UserStatisticsCacheDatabase _userDatabase = new(osuApi);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Scores observer background service started");

        await AddPlayersToObserverList("uz", 50);
        await AddPlayersToObserverList(null, 50);


        await Task.WhenAll([ObserveScoresGetScores(stoppingToken), ObserveScores(stoppingToken)]);
    }

    private async Task ObserveScoresGetScores(CancellationToken stoppingToken)
    {
        await _userDatabase.CacheIfNeeded();
        ILocalization language = new Russian();
        var dailyStatistics = new DailyStatistics(CountryCode.Uzbekistan, DateTime.UtcNow);
        AllDailyStatistics.Add(dailyStatistics);
        long adminTelegramId = (await database.OsuUsers.FirstAsync((m) => m.IsAdmin, cancellationToken: stoppingToken))
            .TelegramId;

        string? cursor = null;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                ScoresResponse? response =
                    await osuApi.Scores.GetScores(new() { CursorString = cursor, Ruleset = Ruleset.Osu });
                if (response == null)
                {
                    logger.LogWarning("GetScores() returned null");
                    continue;
                }

                Score[] uzScores = response.Scores!.Where(m => _userDatabase.ContainsUserStatistics(m.UserId!.Value))
                    .ToArray();
                foreach (var score in uzScores)
                {
                    UserStatistics? userStatistics = await _userDatabase.GetUserStatistics(score.UserId!.Value);
                    if (userStatistics == null)
                    {
                        logger.LogError($"User statistics is null for userId = {score.UserId!.Value}");
                        continue;
                    }

                    dailyStatistics.Scores.Add(score);

                    // ReSharper disable once SimplifyLinqExpressionUseAll
                    if (!dailyStatistics.ActiveUsers.Any(m => m.Id == userStatistics.User!.Id))
                    {
                        dailyStatistics.ActiveUsers.Add(userStatistics.User!);
                    }

                    // ReSharper disable once SimplifyLinqExpressionUseAll
                    if (!dailyStatistics.BeatmapsPlayed.Any(m => m == score.BeatmapId!.Value))
                    {
                        dailyStatistics.BeatmapsPlayed.Add(score.BeatmapId!.Value);
                    }
                }

                // New day => send statistics
                if (uzScores.Length != 0 && uzScores.Last().EndedAt!.Value.ToUniversalTime().Day !=
                    dailyStatistics.DayOfStatistic.Day)
                {
                    int activePlayersCount = dailyStatistics.ActiveUsers.Count;
                    int passedScores = dailyStatistics.Scores.Count;
                    int beatmapsPlayed = dailyStatistics.BeatmapsPlayed.Count;

                    Score? mostPPForScore = dailyStatistics.Scores.MaxBy(m => m.Pp);
                    User? userHavingMostPPForScore =
                        dailyStatistics.ActiveUsers.FirstOrDefault(m => m.Id == mostPPForScore?.UserId);

                    var usersAndTheirScores = dailyStatistics.ActiveUsers.Select(m =>
                    {
                        return (m, dailyStatistics.Scores.Where(s => s.UserId == m.Id).ToArray());
                    }).OrderByDescending(m => { return m.Item2.Length; }).ToArray();

                    var mostPlayedBeatmaps = dailyStatistics.Scores
                        .GroupBy(m => m.BeatmapId!.Value)
                        .OrderByDescending(m => m.Count()).ToArray();

                    string top3ActivePlayers = "";
                    int count = 1;
                    foreach (var us in usersAndTheirScores)
                    {
                        if (count == 3) break;
                        top3ActivePlayers +=
                            $"{count}. <b>{us.m.Username}</b> — {us.Item2.Length} скоров, макс. <i>{us.Item2.Max(m => m.Pp)}pp</i><br>\n";
                        count += 1;
                    }

                    string top3MostPlayedBeatmaps = "";
                    count = 1;
                    foreach (var us in mostPlayedBeatmaps)
                    {
                        if (count == 3) break;

                        GetBeatmapResponse? beatmap = await osuApi.Beatmaps.GetBeatmap(us.Key);
                        if (beatmap == null)
                        {
                            logger.LogError($"Beatmap \"{us.Key}\" not found");
                            continue;
                        }

                        BeatmapsetExtended beatmapsetExtended =
                            await osuApi.Beatmapsets.GetBeatmapset(beatmap.BeatmapExtended!.BeatmapsetId.Value);

                        top3MostPlayedBeatmaps +=
                            $"{count}. (<i>{beatmap.BeatmapExtended!.DifficultyRating}★</i>) {beatmapsetExtended.Title.EncodeHtml()} [{beatmap.BeatmapExtended.Version.EncodeHtml()}] — {us.Count()} траев<br>\n";
                        count += 1;
                    }

                    string sendText = language.send_dailyStatistic.Fill([
                        $"{activePlayersCount}",
                        $"{passedScores}",
                        $"{beatmapsPlayed}",
                        $"{BaseOsuScoreLink}{mostPPForScore?.Id}",
                        $"{mostPPForScore?.Pp}",
                        $"{userHavingMostPPForScore?.GetProfileUrl()}",
                        $"{userHavingMostPPForScore?.Username}",

                        $"{top3ActivePlayers}\n",
                        $"{top3MostPlayedBeatmaps}\n",
                    ]);

                    await botClient.SendMessage(adminTelegramId, sendText,
                        ParseMode.Html, linkPreviewOptions: true, cancellationToken: stoppingToken);

                    dailyStatistics = new DailyStatistics(CountryCode.Uzbekistan, DateTime.UtcNow);
                    AllDailyStatistics.Add(dailyStatistics);
                }

                cursor = response.CursorString;
                await Task.Delay(5000, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                logger.LogWarning("Operation cancelled");
                return;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Unexpected exception");
            }
        }
    }

    private async Task ObserveScores(CancellationToken stoppingToken)
    {
        long adminTelegramId =
            (await database.OsuUsers.FirstAsync((m) => m.IsAdmin, cancellationToken: stoppingToken)).TelegramId;
        Dictionary<int, GetUserScoresResponse> scores = new();
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
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
                        IEnumerable<Score> newScores =
                            userBestScores.Scores.Except(scores[userId].Scores, ScoreComparer);
                        foreach (Score score in newScores)
                        {
                            await botClient.SendMessage(adminTelegramId,
                                $"<b>{score.User?.Username}</b> set a <b>{score.Pp}pp</b> <a href=\"{BaseOsuScoreLink}{score.Id}\">score!</a>",
                                ParseMode.Html, linkPreviewOptions: true, cancellationToken: stoppingToken);
                            await Task.Delay(1000, stoppingToken);
                        }
                    }

                    scores[userId] = userBestScores;
                    await Task.Delay(1000, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                logger.LogWarning("Operation cancelled");
                return;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Unexpected exception");
            }
        }

        logger.LogWarning("Finished its work");
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
            new() { Country = countryCode, Filter = Filter.All });
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
    /// <param name="countryCode">If null, take from global ranking</param>
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