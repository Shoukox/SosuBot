using Microsoft.Extensions.DependencyInjection;
using OsuApi.BanchoV2;
using OsuApi.BanchoV2.Clients.Users.HttpIO;
using OsuApi.BanchoV2.Users.Models;
using SosuBot.Database;
using SosuBot.Extensions;
using SosuBot.Helpers;
using SosuBot.Services.Synchronization;
using SosuBot.TelegramHandlers.Abstract;
using System.Data;
using System.Text.RegularExpressions;
using Telegram.Bot.Types;

namespace SosuBot.TelegramHandlers.Commands;

public class OsuLastBestCommand : CommandBase<Message>
{
    public static readonly string[] Commands = ["/lb", "/lastbest"];
    private BanchoApiV2 _osuApiV2 = null!;
    private ScoreHelper _scoreHelper = null!;
    private CachingHelper _cachingHelper = null!;
    private RateLimiterFactory _rateLimiterFactory = null!;
    private BotContext _database = null!;

    public override async Task BeforeExecuteAsync()
    {
        await base.BeforeExecuteAsync();
        _osuApiV2 = Context.ServiceProvider.GetRequiredService<BanchoApiV2>();
        _scoreHelper = Context.ServiceProvider.GetRequiredService<ScoreHelper>();
        _cachingHelper = Context.ServiceProvider.GetRequiredService<CachingHelper>();
        _rateLimiterFactory = Context.ServiceProvider.GetRequiredService<RateLimiterFactory>();
        _database = Context.ServiceProvider.GetRequiredService<BotContext>();
    }

    public override async Task ExecuteAsync()
    {
        var language = Context.GetLocalization();
        var rateLimiter = _rateLimiterFactory.Get(RateLimiterFactory.RateLimitPolicy.Command);
        if (!await rateLimiter.IsAllowedAsync($"{Context.Update.From!.Id}"))
        {
            await Context.Update.ReplyAsync(Context.BotClient, language.common_rateLimitSlowDown);
            return;
        }
        var chatInDatabase = await _database.TelegramChats.FindAsync(Context.Update.Chat.Id);
        var osuUserInDatabase = await _database.OsuUsers.FindAsync(Context.Update.From!.Id);

        var waitMessage = await Context.Update.ReplyAsync(Context.BotClient, language.waiting);

        // Fake 500ms wait
        await Task.Delay(500);

        var osuUsernameForLastScores = string.Empty;
        var keywordParameters = Context.Update.Text!.GetCommandKeywordParameters()!;
        var parameters = Context.Update.Text!.GetCommandParameters()!.Where(m => !keywordParameters.Contains(m)).ToArray();

        var limit = 1;
        string? ruleset = TextHelper.GetPlaymodeFromParameters(parameters, out parameters)?.ToRuleset();

        //lb
        if (parameters.Length == 0)
        {
            if (osuUserInDatabase is null)
            {
                await waitMessage.EditAsync(Context.BotClient, language.error_userNotSetHimself);
                return;
            }

            osuUsernameForLastScores = osuUserInDatabase.OsuUsername;
            ruleset ??= osuUserInDatabase.OsuMode.ToRuleset();
        }
        //lb 5
        //lb mrekk
        else if (parameters.Length == 1)
        {
            var limitParsed = parameters[0].Length == 1 && int.TryParse(parameters[0][0].ToString(), out limit);
            if (limitParsed)
            {
                if (osuUserInDatabase is null)
                {
                    await waitMessage.EditAsync(Context.BotClient, language.error_userNotSetHimself);
                    return;
                }

                osuUsernameForLastScores = osuUserInDatabase.OsuUsername;
                ruleset ??= osuUserInDatabase.OsuMode.ToRuleset();
            }

            if (!limitParsed) osuUsernameForLastScores = parameters[0];
        }
        //lb mrekk 5
        else if (parameters.Length == 2)
        {
            string parametersJoined = string.Join(" ", parameters);
            string numberAsText = Regex.Match(parametersJoined, @" (\d)").Value;
            if (!int.TryParse(numberAsText, out limit))
            {
                await waitMessage.EditAsync(Context.BotClient, language.error_baseMessage + $"\n{language.last_usage}");
                return;
            }
            osuUsernameForLastScores = Regex.Match(parametersJoined, @"(\S{3,})").Value;
        }
        else
        {
            await waitMessage.EditAsync(Context.BotClient, language.error_argsLength);
            return;
        }

        if (ruleset == null || keywordParameters.Length != 0)
        {
            if (keywordParameters.FirstOrDefault(m => m.StartsWith("mode")) is { } keyword)
            {
                ruleset = keyword.Split('=')[1].ParseToRuleset();
                if (ruleset is null)
                {
                    await waitMessage.EditAsync(Context.BotClient, language.error_modeIncorrect);
                    return;
                }
            }
        }

        // getting osu!player through username
        var userResponse =
            await _osuApiV2.Users.GetUser($"@{osuUsernameForLastScores}", new GetUserQueryParameters());
        if (userResponse is null)
        {
            await waitMessage.EditAsync(Context.BotClient,
                language.error_userNotFound + "\n\n" + language.error_hintReplaceSpaces);
            return;
        }

        osuUsernameForLastScores = userResponse.UserExtend!.Username!;

        // if username was entered, then use as ruleset his (this username) standard ruleset.
        ruleset ??= userResponse.UserExtend!.Playmode!;

        var userBestScores = await _osuApiV2.Users.GetUserScores(userResponse.UserExtend.Id!.Value, ScoreType.Best, new() { Limit = 200, Mode = ruleset });
        var timeSortedUserBestScores = userBestScores!.Scores.Where(m => m.EndedAt > DateTime.UtcNow.Date.AddDays(-DateTime.UtcNow.Date.Day + 1)).ToArray();
        var lastBestScores = userBestScores!.Scores.OrderByDescending(m => m.EndedAt).Take(limit).ToArray();

        BeatmapExtended[] beatmaps = lastBestScores
            .Select(async score => await _cachingHelper.GetOrCacheBeatmap(score.Beatmap!.Id!.Value, _osuApiV2))
            .Select(t => t.Result).ToArray()!;

        var textToSend =
            $"{UserHelper.GetUserProfileUrlWrappedInUsernameString(userResponse.UserExtend!.Id.Value, $"<b>{osuUsernameForLastScores}</b>")}\n\n";
        for (var i = 0; i <= lastBestScores.Length - 1; i++)
        {
            var score = lastBestScores[i];
            var beatmap = beatmaps[i];
            int scoreIndexInBestScores = Array.IndexOf(userBestScores.Scores, score) + 1;

            textToSend += $"{scoreIndexInBestScores}. " + LocalizationMessageHelper.CommandScore(language,
                $"{_scoreHelper.GetScoreRankEmoji(score.Rank)}{_scoreHelper.ParseScoreRank(score.Rank!)}",
                $"{beatmap.Url}",
                $"{score.Beatmapset!.Title.EncodeHtml()}",
                $"{beatmap.Version.EncodeHtml()}",
                $"{beatmap.Status}",
                $"{_scoreHelper.GetScoreStatisticsText(score.Statistics!, ruleset.ParseRulesetToPlaymode())}",
                $"{score.Statistics!.Miss}",
                $"{_scoreHelper.GetFormattedNumConsideringNull(score.Accuracy * 100, round: false)}",
                $"{_scoreHelper.GetModsText(score.Mods!)}",
                $"{score.MaxCombo}",
                $"{beatmap.MaxCombo}",
                $"{_scoreHelper.GetFormattedNumConsideringNull(score.Pp)}",
                $"({score.EndedAt!.Value:dd.MM.yyyy HH:mm}) {_scoreHelper.GetScoreUrlWrappedInString(score.Id!.Value, "link")}"
            );
        }

        await waitMessage.EditAsync(Context.BotClient, textToSend, splitValue: "\n\n");
    }
}


