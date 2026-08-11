using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OsuApi.BanchoV2;
using OsuApi.BanchoV2.Clients.Beatmaps.HttpIO;
using OsuApi.BanchoV2.Clients.Users.HttpIO;
using OsuApi.BanchoV2.Models;
using OsuApi.BanchoV2.Users.Models;
using SosuBot.Database;
using SosuBot.Database.Models;
using SosuBot.Calculators.Official;
using SosuBot.Extensions;
using SosuBot.Helpers;
using SosuBot.Localization;
using SosuBot.PerformanceCalculator;
using SosuBot.Services;
using SosuBot.Services.Synchronization;
using SosuBot.TelegramHandlers.Abstract;
using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace SosuBot.TelegramHandlers.Commands;

public class OsuLastCommand(bool onlyPassed = false, bool sendCover = false) : CommandBase<Message>
{
    public static readonly string[] Commands = ["/ll"];
    public static readonly string Description = "[osuname] [count] последние сыгранные игры";
    private bool _onlyPassed;
    private BanchoApiV2 _osuApiV2 = null!;
    private ScoreHelper _scoreHelper = null!;
    private CachingHelper _cachingHelper = null!;
    private RateLimiterFactory _rateLimiterFactory = null!;
    private BeatmapsService _beatmapsService = null!;
    private BotContext _database = null!;
    private ILogger<OsuLastCommand> _logger = null!;
    private OfficialPerformanceHelper _officialPerformanceHelper = null!;

    public override async Task BeforeExecuteAsync()
    {
        await base.BeforeExecuteAsync();
        _onlyPassed = onlyPassed;
        _osuApiV2 = Context.ServiceProvider.GetRequiredService<BanchoApiV2>();
        _scoreHelper = Context.ServiceProvider.GetRequiredService<ScoreHelper>();
        _cachingHelper = Context.ServiceProvider.GetRequiredService<CachingHelper>();
        _rateLimiterFactory = Context.ServiceProvider.GetRequiredService<RateLimiterFactory>();
        _beatmapsService = Context.ServiceProvider.GetRequiredService<BeatmapsService>();
        _database = Context.ServiceProvider.GetRequiredService<BotContext>();
        _logger = Context.ServiceProvider.GetRequiredService<ILogger<OsuLastCommand>>();
        _officialPerformanceHelper = Context.ServiceProvider.GetRequiredService<OfficialPerformanceHelper>();
    }

    public override async Task ExecuteAsync()
    {
        ILocalization language = Context.GetLocalization();
        TokenBucketRateLimiter rateLimiter = _rateLimiterFactory.Get(RateLimiterFactory.RateLimitPolicy.Command);
        if (!await rateLimiter.IsAllowedAsync($"{Context.Update.From!.Id}"))
        {
            await Context.Update.ReplyAsync(Context.BotClient, language.common_rateLimitSlowDown);
            return;
        }
        TelegramChat? chatInDatabase = await _database.TelegramChats.FindAsync(Context.Update.Chat.Id);
        OsuUser? osuUserInDatabase = await _database.OsuUsers.FindAsync(Context.Update.From!.Id);

        Message waitMessage = await Context.Update.ReplyAsync(Context.BotClient, language.waiting);

        // Fake 500ms wait
        await Task.Delay(500);

        var osuUsernameForLastScores = string.Empty;
        var keywordParameters = Context.Update.Text!.GetCommandKeywordParameters()!;
        var parameters = Context.Update.Text!.GetCommandParameters()!.Where(m => !keywordParameters.Contains(m)).ToArray();

        var limit = 1;
        string? ruleset = TextHelper.GetPlaymodeFromParameters(parameters, out parameters)?.ToRuleset();

        //l
        if (parameters.Length == 0)
        {
            if (osuUserInDatabase is null)
            {
                await waitMessage.EditAsync(Context.BotClient,
                    _onlyPassed ? language.command_lastPassed_usage : language.last_usage);
                return;
            }

            osuUsernameForLastScores = osuUserInDatabase.OsuUsername;
            ruleset ??= osuUserInDatabase.OsuMode.ToRuleset();
        }
        //l 5
        //l mrekk
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
        //l mrekk 5
        else if (parameters.Length == 2)
        {
            string parametersJoined = string.Join(" ", parameters);
            string numberAsText = Regex.Match(parametersJoined, @" (\d)").Value;
            if (!int.TryParse(numberAsText, out limit))
            {
                await waitMessage.EditAsync(Context.BotClient,
                    _onlyPassed ? language.command_lastPassed_usage : language.last_usage);
                return;
            }
            osuUsernameForLastScores = Regex.Match(parametersJoined, @"(\S{3,})").Value;
        }
        else
        {
            await waitMessage.EditAsync(Context.BotClient,
                _onlyPassed ? language.command_lastPassed_usage : language.last_usage);
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
        _logger.LogInformation("[/last] Get user from osu!api");
        GetUserResponse? userResponse =
            await _osuApiV2.Users.GetUser($"@{osuUsernameForLastScores}", new GetUserQueryParameters());
        _logger.LogInformation("[/last] End of get user from osu!api");
        if (userResponse is null)
        {
            await waitMessage.EditAsync(Context.BotClient,
                language.error_userNotFound + "\n\n" + language.error_hintReplaceSpaces);
            return;
        }

        osuUsernameForLastScores = userResponse.UserExtend!.Username!;

        // if username was entered, then use as ruleset his (this username) standard ruleset.
        ruleset ??= userResponse.UserExtend!.Playmode!;

        _logger.LogInformation("[/last] Get user scores from osu!api");
        GetUserScoresResponse? lastScoresResponse = await _osuApiV2.Users.GetUserScores(userResponse.UserExtend!.Id.Value,
            ScoreType.Recent,
            new GetUserScoreQueryParameters
            { IncludeFails = Convert.ToInt32(!_onlyPassed), Limit = limit, Mode = ruleset });
        _logger.LogInformation("[/last] End of get user scores from osu!api");
        if (lastScoresResponse!.Scores.Length == 0)
        {
            await waitMessage.EditAsync(Context.BotClient,
                LocalizationMessageHelper.ErrorNoPreviousScores(language, ruleset.ParseRulesetToGamemode()));
            return;
        }

        Score[] lastScores = lastScoresResponse.Scores;
        BeatmapExtended[] beatmaps = lastScores
            .Select(async score => await _cachingHelper.GetOrCacheBeatmap(score.Beatmap!.Id!.Value, _osuApiV2))
            .Select(t => t.Result).ToArray()!;

        var textToSend =
            $"<b>{UserHelper.GetUserProfileUrlWrappedInUsernameString(userResponse.UserExtend!.Id.Value, osuUsernameForLastScores)}</b> (<i>{ruleset.ParseRulesetToGamemode()}</i>)\n\n";

        var playmode = (Playmode)lastScores[0].RulesetId!;
        var beatmapsetIdOfFirstScore = beatmaps[0].BeatmapsetId!.Value;
        for (var i = 0; i <= lastScores.Length - 1; i++)
        {
            Score? score = await _cachingHelper.GetOrCacheScore(lastScores[i].Id!.Value, _osuApiV2);
            BeatmapExtended beatmap = beatmaps[i];

            var hitobjectsSum = beatmap.CountCircles + beatmap.CountSliders + beatmap.CountSpinners;
            bool beatmapContainsTooManyHitObjects = hitobjectsSum >= 20000;
            OsuApi.BanchoV2.Models.Mod[] mods = score!.Mods ?? [];

            if (i == 0) chatInDatabase!.LastBeatmapId = beatmap.Id;

            var passed = score.Passed!.Value;

            // Calculate pp
            PPCalculationResult? currentPerformanceResult = null;
            PPCalculationResult? fcPerformanceResult = null;
            if (!beatmapContainsTooManyHitObjects)
            {
                using Stream beatmapFile = await _beatmapsService.DownloadOrCacheBeatmapAsync(beatmap.Id!.Value,
                    Context.CancellationToken);

                _logger.LogInformation("[/last] Calculating pp");
                OfficialScoreCalculation calculation = await _officialPerformanceHelper.CalculateScoreAsync(
                    beatmapFile,
                    score,
                    playmode,
                    calculateCurrent: true,
                    cancellationToken: Context.CancellationToken);
                currentPerformanceResult = calculation.Current;
                fcPerformanceResult = calculation.IfFc;
                _logger.LogInformation("[/last] End of calculating pp");
            }
            var scoreRank = _scoreHelper.GetScoreRankEmoji(score.Rank!, score.Passed!.Value) +
                            _scoreHelper.ParseScoreRank(score.Passed!.Value ? score.Rank! : "F");
            bool lastScoresContainsOnlyOneScore = lastScores.Length == 1;
            string counterText = lastScoresContainsOnlyOneScore ? "" : $"{i + 1}. ";
            string optionalNewLine = lastScoresContainsOnlyOneScore ? "\n" : "";
            double? scorePp = currentPerformanceResult?.PP ?? score.Pp;
            if (scorePp is { } scorePpValue && double.IsNaN(scorePpValue)) scorePp = null;

            double? scorePpIfFc = fcPerformanceResult?.PP;
            double? accuracyIfFc = fcPerformanceResult is null
                ? null
                : playmode is Playmode.Mania or Playmode.Taiko
                    ? 1
                    : score.Accuracy ?? fcPerformanceResult.CalculatedAccuracy;
            // Beatmap max combo from pp calculation (or use beatmap.MaxCombo if null)
            int? beatmapMaxCombo = fcPerformanceResult?.BeatmapMaxCombo;
            if (beatmap.ModeInt == (int)playmode)
            {
                beatmapMaxCombo ??= beatmap.MaxCombo;
            }

            // Calculate diff rating
            double? difficultyRating = fcPerformanceResult?.DifficultyAttributes.StarRating;
            if (difficultyRating == null)
            {
                GetBeatmapAttributesResponse? beatmapAttributesResponse = await _osuApiV2.Beatmaps.GetBeatmapAttributes(beatmap.Id.Value, new() { RulesetId = ((int)playmode).ToString(), Mods = mods });

                int? maxCombo = beatmapAttributesResponse?.DifficultyAttributes?.MaxCombo;
                if (maxCombo != null && maxCombo != 0)
                {
                    beatmapMaxCombo ??= maxCombo;
                }
                difficultyRating = beatmapAttributesResponse?.DifficultyAttributes?.StarRating;
            }


            // If fc, then curPp = fcPp
            bool isFc = score.MaxCombo == beatmapMaxCombo;
            if (isFc && playmode == Playmode.Osu)
            {
                scorePpIfFc = scorePp;
                accuracyIfFc ??= score.Accuracy;
            }

            string scorePpText = _scoreHelper.GetFormattedNumConsideringNull(scorePp);

            string scoreIfFcPpText =
                $"{_scoreHelper.GetFormattedNumConsideringNull(scorePpIfFc)}pp if {_scoreHelper.GetFormattedNumConsideringNull(accuracyIfFc * 100, round: false)}% FC";

            var scoreEndedMinutesAgoText = LocalizationMessageHelper.LastScoreEndedAgo(language, score.EndedAt!.Value);

            // A passed score has completed the map. For a fail, combo is the
            // stable cross-ruleset completion approximation available without
            // reparsing the map a second time for hit-result totals.
            double? completion = passed
                ? 100
                : beatmapMaxCombo is > 0 && score.MaxCombo is >= 0
                    ? Math.Clamp((double)score.MaxCombo.Value / beatmapMaxCombo.Value * 100, 0, 100)
                    : null;

            string globalRankText = "";
            if (score.RankGlobal != null && score.RankGlobal is > 0 and <= 2000)
            {
                globalRankText = $"<b>Global #{score.RankGlobal}</b>\n";
            }

            textToSend += LocalizationMessageHelper.CommandLast(language,
                $"{globalRankText}",
                $"{counterText}",
                $"{scoreRank}",
                $"{beatmap.Id}",
                $"{score.Beatmapset?.Title.EncodeHtml()}",
                $"{beatmap.Version.EncodeHtml()}",
                $"{beatmap.Status}",
                $"{_scoreHelper.GetFormattedNumConsideringNull(difficultyRating, format: "N2", round: false)}",
                $"{_scoreHelper.GetScoreStatisticsText(score.Statistics!, playmode)}",
                $"{score.Statistics!.Miss}",
                $"{_scoreHelper.GetFormattedNumConsideringNull(score.Accuracy * 100, round: false)}",
                optionalNewLine,
                $"{_scoreHelper.GetModsText(mods)}",
                $"{score.MaxCombo}",
                $"{_scoreHelper.GetFormattedNumConsideringNull(beatmapMaxCombo, format: "F0")}",
                $"{scorePpText}",
                $"{scoreIfFcPpText}",
                $"{_scoreHelper.GetScoreUrlWrappedInString(score.Id!.Value, "link")}",
                $"{scoreEndedMinutesAgoText}",
                $"{_scoreHelper.GetFormattedNumConsideringNull(completion, format: "N1")}"
            );

            if (lastScoresContainsOnlyOneScore)
            {
                if (score.HasReplay == true)
                {
                    textToSend += $"\n\n{language.score_replayAvailable}";
                }
            }

            if (beatmapContainsTooManyHitObjects)
                textToSend += $"\n{language.last_tooManyObjectsLimitedInfo}";

            textToSend += "\n\n";
        }

        if (sendCover)
        {
            // Get beatmapset cover from cache
            InputFile cover = await _cachingHelper.GetOrCacheBeatmapsetCover(beatmapsetIdOfFirstScore);

            try
            {
                await Context.BotClient.EditMessageMedia(waitMessage.Chat.Id, waitMessage.Id, new InputMediaPhoto(cover) { Caption = textToSend, ParseMode = Telegram.Bot.Types.Enums.ParseMode.Html });
            }
            catch
            {
                await waitMessage.EditAsync(Context.BotClient, textToSend);
            }
        }
        else
        {
            await waitMessage.EditAsync(Context.BotClient, textToSend);
        }
    }

}
