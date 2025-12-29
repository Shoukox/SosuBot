using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using osu.Game.Rulesets.Osu.Mods;
using OsuApi.V2;
using OsuApi.V2.Clients.Users.HttpIO;
using OsuApi.V2.Models;
using OsuApi.V2.Users.Models;
using SosuBot.Database;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.Helpers;
using SosuBot.Helpers.OutputText;
using SosuBot.Helpers.Types;
using SosuBot.Localization;
using SosuBot.Localization.Languages;
using SosuBot.PerformanceCalculator;
using SosuBot.Services.BackgroundServices;
using SosuBot.Services.Handlers.Abstract;
using System.Diagnostics;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Country = SosuBot.Helpers.Country;

namespace SosuBot.Services.Handlers.Commands;

public sealed class CustomCommand : CommandBase<Message>
{
    public static readonly string[] Commands = ["/c"];
    private ILogger<CustomCommand> _logger = null!;
    private OpenAiService _openaiService = null!;
    private ApiV2 _osuApiV2 = null!;
    private ScoreHelper _scoreHelper = null!;
    private BeatmapsService _beatmapsService = null!;

    public override Task BeforeExecuteAsync()
    {
        _openaiService = Context.ServiceProvider.GetRequiredService<OpenAiService>();
        _osuApiV2 = Context.ServiceProvider.GetRequiredService<ApiV2>();
        _scoreHelper = Context.ServiceProvider.GetRequiredService<ScoreHelper>();
        _beatmapsService = Context.ServiceProvider.GetRequiredService<BeatmapsService>();
        _logger = Context.ServiceProvider.GetRequiredService<ILogger<CustomCommand>>();
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
                    _scoreHelper.GetModsText(m.Mods!.Where(mod =>
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
                    $"{pair.Key} - max. {_scoreHelper.GetScoreUrlWrappedInString(pair.Item2.Id!.Value, $"{pair.Item2.Pp:N2}pp")}{lazer} by {UserHelper.GetUserProfileUrlWrappedInUsernameString(pair.Item2.UserId!.Value, pair.Item2.User!.Username!)}\n";
            }

            await waitMessage.EditAsync(Context.BotClient, sendText, splitValue: "\n");
        }
        else if (parameters[0] == "add-daily-stats-from-last")
        {
            ILocalization language = new Russian();
            var waitMessage = await Context.Update.ReplyAsync(Context.BotClient, language.waiting);

            var dailyStats = Context.Database.DailyStatistics.OrderBy(m => m.Id).Last();
            Task<(int newUsers, int newScores, int newBeatmaps)>[] resultTasks =
            [
                _scoreHelper.UpdateDailyStatisticsFromLast(_osuApiV2, Playmode.Osu, dailyStats),
                _scoreHelper.UpdateDailyStatisticsFromLast(_osuApiV2, Playmode.Taiko, dailyStats),
                _scoreHelper.UpdateDailyStatisticsFromLast(_osuApiV2, Playmode.Catch, dailyStats),
                _scoreHelper.UpdateDailyStatisticsFromLast(_osuApiV2, Playmode.Mania, dailyStats)
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
            var dailyStatistics = Context.Database.DailyStatistics.OrderBy(m => m.Id).Last();
            var passedStdScores = dailyStatistics.Scores.Where(m => m.ScoreJson.ModeInt == (int)Playmode.Osu).ToList();
            var removed = dailyStatistics.Scores.RemoveAll(m =>
            {
                return passedStdScores.Any(std => std.ScoreId == m.ScoreId && std.ScoreJson.ModeInt != m.ScoreJson.ModeInt && m.ScoreJson.ModeInt != 0);
            });
            /*
             * 23.10.2025 02:00 UTC => alles bis 23.10.2025 07:00 UZ
             */
            var tashkentToday = DateTime.UtcNow.ChangeTimezone(Country.Uzbekistan).Date;
            _logger.LogInformation(tashkentToday.ToString("g"));
            removed += dailyStatistics.Scores.RemoveAll(m =>
                m.ScoreJson.EndedAt!.Value.ChangeTimezone(Country.Uzbekistan) < tashkentToday);
            await waitMessage.EditAsync(Context.BotClient, $"Scores removed: {removed}");
        }
        else if (parameters[0] == "test1")
        {
            var ppCalculator = new PPCalculator();

            int beatmapId = 970048;
            Stopwatch sw = Stopwatch.StartNew();

            int count = 1000;
            Parallel.For(0, count, m =>
            {
                var beatmapFile = _beatmapsService.DownloadOrCacheBeatmap(beatmapId).Result;
                var calculatedPp = ppCalculator.CalculatePpAsync(
                    beatmapId: beatmapId,
                    beatmapFile: beatmapFile,
                    accuracy: 0.9889,
                    scoreMaxCombo: 1466,
                    passed: true,
                    scoreMods: [new OsuModClassic()],
                    scoreStatistics: null,
                    rulesetId: (int)Playmode.Osu,
                    cancellationToken: Context.CancellationToken).Result;
            });
            sw.Stop();

            await Context.Update.ReplyAsync(Context.BotClient, $"pp calculation of {count} scores: {sw.ElapsedMilliseconds}ms");
            //for (var i = 1; i <= 1000; i++)
            //{
            //    var beatmapFile = await _beatmapsService.DownloadOrCacheBeatmap(beatmapId);
            //    var calculatedPp = await ppCalculator.CalculatePpAsync(
            //        beatmapId: beatmapId, 
            //        beatmapFile: beatmapFile,
            //        accuracy: 0.9889,
            //        scoreMaxCombo: 1466,
            //        passed: true,
            //        scoreMods: [new OsuModClassic()],
            //        scoreStatistics: null,
            //        rulesetId: (int)Playmode.Osu,
            //        cancellationToken: Context.CancellationToken);
            //    await Task.Delay(1000);
            //}
        }
        else if (parameters[0] == "fix28112025_distinctscores")
        {
            // this id is raisy
            var lastDbDailyStats = Context.Database.DailyStatistics.OrderBy(m => m.Id).Last();
            int oldCount = lastDbDailyStats.Scores.Count;
            lastDbDailyStats.Scores = lastDbDailyStats.Scores.DistinctBy(m => m.ScoreId).ToList();
            await Context.Update.ReplyAsync(Context.BotClient, $"Scores old count: {oldCount}\nScores new count: {lastDbDailyStats.Scores.Count}");
        }
        else if (parameters[0] == "add-from-sqlite")
        {
            await using SqliteConnection sqlite = new SqliteConnection("Data Source=bot.db");
            await sqlite.OpenAsync();
            (int addedOsuUsers, int addedTelegramChats) = (0, 0);

            await using var osuUsersCommand = sqlite.CreateCommand();
            osuUsersCommand.CommandText = "SELECT * FROM OsuUsers";
            await using var osuUsersReader = await osuUsersCommand.ExecuteReaderAsync();
            while (await osuUsersReader.ReadAsync())
            {
                var telegramId = osuUsersReader.GetInt64(osuUsersReader.GetOrdinal("TelegramId"));
                var osuUserId = osuUsersReader.GetInt64(osuUsersReader.GetOrdinal("OsuUserId"));
                var osuUsername = osuUsersReader.GetString(osuUsersReader.GetOrdinal("OsuUsername"));
                var osuMode =
                    osuUsersReader.GetInt32(osuUsersReader.GetOrdinal("OsuMode")); // or string depending on schema
                var isAdmin =
                    osuUsersReader.GetBoolean(
                        osuUsersReader.GetOrdinal("IsAdmin")); // only works if column type is boolean
                var stdPp = osuUsersReader.GetDouble(osuUsersReader.GetOrdinal("StdPPValue"));
                var taikoPp = osuUsersReader.GetDouble(osuUsersReader.GetOrdinal("TaikoPPValue"));
                var catchPp = osuUsersReader.GetDouble(osuUsersReader.GetOrdinal("CatchPPValue"));
                var maniaPp = osuUsersReader.GetDouble(osuUsersReader.GetOrdinal("ManiaPPValue"));

                if (Context.Database.OsuUsers.FirstOrDefault(m => m.TelegramId == telegramId) is { } osuUser)
                {
                    osuUser.OsuUserId = osuUserId;
                    osuUser.OsuUsername = osuUsername;
                    osuUser.OsuMode = (Playmode)osuMode;
                    osuUser.StdPPValue = stdPp;
                    osuUser.TaikoPPValue = taikoPp;
                    osuUser.CatchPPValue = catchPp;
                    osuUser.ManiaPPValue = maniaPp;
                }
                else
                {
                    osuUser = new Database.Models.OsuUser()
                    {
                        TelegramId = telegramId,
                        OsuUserId = osuUserId,
                        OsuUsername = osuUsername,
                        OsuMode = (Playmode)osuMode,
                        StdPPValue = stdPp,
                        TaikoPPValue = taikoPp,
                        CatchPPValue = catchPp,
                        ManiaPPValue = maniaPp
                    };
                    Context.Database.OsuUsers.Add(osuUser);
                    addedOsuUsers += 1;
                }

                await Context.Database.SaveChangesAsync();
            }

            await using var telegramChatsCommand = sqlite.CreateCommand();
            telegramChatsCommand.CommandText = "SELECT * FROM TelegramChats";
            await using var telegramChatsReader = await telegramChatsCommand.ExecuteReaderAsync();
            while (await telegramChatsReader.ReadAsync())
            {
                var chatId = telegramChatsReader.GetInt64(telegramChatsReader.GetOrdinal("ChatId"));
                var chatMembers = telegramChatsReader.GetValue(telegramChatsReader.GetOrdinal("ChatMembers"));
                var excl = telegramChatsReader.GetValue(telegramChatsReader.GetOrdinal("ExcludeFromChatstats"));
                var lastBeatmapId = telegramChatsReader.GetValue(telegramChatsReader.GetOrdinal("LastBeatmapId"));

                List<long>? parsedChatMembers;
                if (chatMembers is DBNull || chatMembers.ToString()!.Length == 2) parsedChatMembers = null;
                else
                    parsedChatMembers = ((string)chatMembers).Replace("[", "").Replace("]", "").Split(',')
                        .Select(long.Parse).ToList();

                List<long>? parsedExcl;
                if (excl is DBNull || excl.ToString()!.Length == 2) parsedExcl = null;
                else
                    parsedExcl = ((string)excl).Replace("[", "").Replace("]", "").Split(',').Select(long.Parse)
                        .ToList();

                int? parsedLastBeatmapId;
                if (lastBeatmapId is DBNull || lastBeatmapId.ToString()!.Length == 2) parsedLastBeatmapId = null;
                else parsedLastBeatmapId = lastBeatmapId is DBNull ? null : Convert.ToInt32(lastBeatmapId);

                if (Context.Database.TelegramChats.FirstOrDefault(m => m.ChatId == chatId) is { } tgChat)
                {
                    tgChat.ChatMembers = parsedChatMembers;
                    tgChat.ExcludeFromChatstats = parsedExcl;
                    tgChat.LastBeatmapId = parsedLastBeatmapId;
                }
                else
                {
                    tgChat = new Database.Models.TelegramChat()
                    {
                        ChatId = chatId,
                        ChatMembers = parsedChatMembers,
                        ExcludeFromChatstats = parsedExcl,
                        LastBeatmapId = parsedLastBeatmapId
                    };
                    Context.Database.TelegramChats.Add(tgChat);
                    addedTelegramChats += 1;
                }

                await Context.Database.SaveChangesAsync();
            }

            await Context.Update.ReplyAsync(Context.BotClient,
                $"Added osuUsers: {addedOsuUsers}\nAdded telegramChats: {addedTelegramChats}");
        }
        else if (parameters[0] == "replace-daily-stats-into-postgres19122025")
        {
            ILocalization language = new Russian();
            var waitMessage = await Context.Update.ReplyAsync(Context.BotClient, language.waiting);

            if (Context.Database.DailyStatistics.Count() != 0)
            {
                await waitMessage.EditAsync(Context.BotClient, $"The daily stats are not empty. Declining.");
                return;
            }

            Context.Database.UserEntity.ExecuteDelete();
            Context.Database.ScoreEntity.ExecuteDelete();
            Context.Database.DailyStatistics.ExecuteDelete();
            Context.Database.SaveChanges();

            int oldCount = Context.Database.DailyStatistics.Count();
            var allUsersFromStatistics = ScoresObserverBackgroundService.AllDailyStatistics.SelectMany(m => m.ActiveUsers).DistinctBy(m => m.UserId).ToList();
            Context.Database.UserEntity.AddRange(allUsersFromStatistics);
            Context.Database.SaveChanges();

            var allScoresFromStatistics = ScoresObserverBackgroundService.AllDailyStatistics.SelectMany(m => m.Scores).DistinctBy(m => m.ScoreId).ToList();
            Context.Database.ScoreEntity.AddRange(allScoresFromStatistics);
            Context.Database.SaveChanges();

            Context.Database.DailyStatistics.AddRange(ScoresObserverBackgroundService.AllDailyStatistics.Select(m => new Database.Models.DailyStatistics()
            {
                CountryCode = m.CountryCode,
                DayOfStatistic = m.DayOfStatistic,
                ActiveUsers = m.ActiveUsers.Select(u => allUsersFromStatistics.First(m => m.UserId == u.UserId)).ToList(),
                BeatmapsPlayed = m.BeatmapsPlayed,
                Scores = m.Scores.Select(s => allScoresFromStatistics.First(m => m.ScoreId == s.ScoreId)).ToList(),
            }));
            Context.Database.SaveChanges();
            int newCount = Context.Database.DailyStatistics.Count();
            await waitMessage.EditAsync(Context.BotClient, $"Added {newCount - oldCount} new daily stats");
        }
        else if (parameters[0] == "fix-daily-stats19122025")
        {
            ILocalization language = new Russian();
            var waitMessage = await Context.Update.ReplyAsync(Context.BotClient, language.waiting);

            var scores = Context.Database.ScoreEntity.ToList();
            foreach (var score in scores)
            {
                score.ScoreId = score.ScoreJson.Id!.Value;
            }

            var users = Context.Database.UserEntity.ToList();
            foreach (var user in users)
            {
                user.UserId = user.UserJson.Id!.Value;
            }

            await waitMessage.EditAsync(Context.BotClient, $"Done");
        }
        else if (parameters[0] == "test2912")
        {
            long goal = -1001384452437;
            long from = -1002693455476;

            var chatGoal = Context.Database.TelegramChats.Find(goal);
            var chatFrom = Context.Database.TelegramChats.Find(from);

            chatGoal!.ChatMembers = chatFrom!.ChatMembers;
            chatGoal.ExcludeFromChatstats = chatFrom.ExcludeFromChatstats;
        }
        else if (parameters[0] == "remove_left_chatmembers")
        {
            ILocalization language = new Russian();
            var waitMessage = await Context.Update.ReplyAsync(Context.BotClient, language.waiting);

            var chats = Context.Database.TelegramChats.Where(m => m.ChatMembers != null && m.ChatId < 0).ToList();
            var chatsToDelete = new List<TelegramChat>();
            foreach (var chat in chats)
            {
                var usersToDelete = new List<long>();
                foreach (var member in chat.ChatMembers!)
                {
                    try
                    {
                        if (await Context.BotClient.GetChatMember(chat.ChatId, member) is { IsInChat: false })
                        {
                            usersToDelete.Add(member);
                        }
                    }
                    catch (ApiRequestException ex) when (ex.Message.Contains("chat not found") || ex.Message.Contains("PARTICIPANT_ID_INVALID"))
                    {
                        chatsToDelete.Add(chat);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error");
                    }
                    await Task.Delay(500);
                }

                chat.ChatMembers.RemoveAll(m => usersToDelete.Contains(m));
            }
            Context.Database.TelegramChats.RemoveRange(chatsToDelete);

            await Context.Database.SaveChangesAsync();
            await waitMessage.EditAsync(Context.BotClient, $"Done");
        }
    }
}