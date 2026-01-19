using Humanizer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using osu.Game.Rulesets.Scoring;
using OsuApi.V2;
using OsuApi.V2.Clients.Users.HttpIO;
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
    public static readonly string[] Commands = ["/last", "/l"];
    private bool _onlyPassed;
    private ApiV2 _osuApiV2 = null!;
    private ScoreHelper _scoreHelper = null!;
    private CachingHelper _cachingHelper = null!;
    private RateLimiterFactory _rateLimiterFactory = null!;
    private BeatmapsService _beatmapsService = null!;
    private BotContext _database = null!;
    private ILogger<OsuLastCommand> _logger = null!;

    public override async Task BeforeExecuteAsync()
    {
        await base.BeforeExecuteAsync();
        _onlyPassed = onlyPassed;
        _osuApiV2 = Context.ServiceProvider.GetRequiredService<ApiV2>();
        _scoreHelper = Context.ServiceProvider.GetRequiredService<ScoreHelper>();
        _cachingHelper = Context.ServiceProvider.GetRequiredService<CachingHelper>();
        _rateLimiterFactory = Context.ServiceProvider.GetRequiredService<RateLimiterFactory>();
        _beatmapsService = Context.ServiceProvider.GetRequiredService<BeatmapsService>();
        _database = Context.ServiceProvider.GetRequiredService<BotContext>();
        _logger = Context.ServiceProvider.GetRequiredService<ILogger<OsuLastCommand>>();
    }

    public override async Task ExecuteAsync()
    {
        var rateLimiter = _rateLimiterFactory.Get(RateLimiterFactory.RateLimitPolicy.Command);
        if (!await rateLimiter.IsAllowedAsync($"{Context.Update.From!.Id}"))
        {
            await Context.Update.ReplyAsync(Context.BotClient, "Давай не так быстро!");
            return;
        }

        ILocalization language = new Russian();
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

        //l
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
                await waitMessage.EditAsync(Context.BotClient, language.error_baseMessage + "\n/last nickname count\n/last Shoukko 5");
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
        _logger.LogInformation("[/last] Get user from osu!api");
        var userResponse =
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
        var lastScoresResponse = await _osuApiV2.Users.GetUserScores(userResponse.UserExtend!.Id.Value,
            ScoreType.Recent,
            new GetUserScoreQueryParameters
            { IncludeFails = Convert.ToInt32(!_onlyPassed), Limit = limit, Mode = ruleset });
        _logger.LogInformation("[/last] End of get user scores from osu!api");
        if (lastScoresResponse!.Scores.Length == 0)
        {
            await waitMessage.EditAsync(Context.BotClient,
                language.error_noPreviousScores.Fill([ruleset.ParseRulesetToGamemode()]));
            return;
        }

        var lastScores = lastScoresResponse.Scores;
        BeatmapExtended[] beatmaps = lastScores
            .Select(async score => await _cachingHelper.GetOrCacheBeatmap(score.Beatmap!.Id!.Value, _osuApiV2))
            .Select(t => t.Result).ToArray()!;
        var ppCalculator = new PPCalculator();

        var textToSend =
            $"<b>{UserHelper.GetUserProfileUrlWrappedInUsernameString(userResponse.UserExtend!.Id.Value, osuUsernameForLastScores)}</b> (<i>{ruleset.ParseRulesetToGamemode()}</i>)\n\n";

        var playmode = (Playmode)lastScores[0].RulesetId!;
        var beatmapsetIdOfFirstScore = beatmaps[0].BeatmapsetId!.Value;
        for (var i = 0; i <= lastScores.Length - 1; i++)
        {
            var score = await _cachingHelper.GetOrCacheScore(lastScores[i].Id!.Value, _osuApiV2);
            var beatmap = beatmaps[i];

            var hitobjectsSum = beatmap.CountCircles + beatmap.CountSliders + beatmap.CountSpinners;
            bool beatmapContainsTooManyHitObjects = hitobjectsSum >= 20000;
            var mods = score!.Mods!;

            if (i == 0) chatInDatabase!.LastBeatmapId = beatmap.Id;

            var passed = score.Passed!.Value;
            var scoreStatistics = score.Statistics!.ToStatistics();

            // Get osu! mods of the score
            var scoreMods = mods.ToOsuMods(playmode);

            // Get score statistics for fc
            _logger.LogInformation("[/last] Get score statistics for fc");
            Dictionary<HitResult, int>? scoreStatisticsIfFc = null;
            if (passed && playmode is not Playmode.Mania and not Playmode.Catch)
            {
                scoreStatisticsIfFc = new Dictionary<HitResult, int>()
                {
                    {
                        HitResult.Great,
                        scoreStatistics.GetValueOrDefault(HitResult.Great)
                    },
                    { HitResult.Good, scoreStatistics.GetValueOrDefault(HitResult.Good) },
                    { HitResult.Ok, scoreStatistics.GetValueOrDefault(HitResult.Ok) },
                    {
                        HitResult.Meh,
                        scoreStatistics.GetValueOrDefault(HitResult.Meh)
                    },
                    { HitResult.Miss, 0 }
                };
            }
            _logger.LogInformation("[/last] End of get score statistics for fc");

            // Calculate pp
            PPResult? calculatedPp = new PPResult() { Current = null, IfFC = null };
            if (!beatmapContainsTooManyHitObjects)
            {
                var beatmapFile = await _beatmapsService.DownloadOrCacheBeatmap(beatmap.Id!.Value);
                _logger.LogInformation("[/last] Calculating pp");
                calculatedPp = new PPResult
                {
                    Current = score.Pp != null ? null :
                        await ppCalculator.CalculatePpAsync(
                             beatmapId: beatmap.Id.Value,
                             beatmapFile: beatmapFile,
                             accuracy: (double)score.Accuracy!,
                             scoreMaxCombo: score.MaxCombo!.Value,
                             passed: passed,
                             scoreMods: scoreMods,
                             scoreStatistics: scoreStatistics,
                             rulesetId: (int)playmode,
                             cancellationToken: Context.CancellationToken),

                    IfFC = await ppCalculator.CalculatePpAsync(
                             beatmapId: beatmap.Id.Value,
                             beatmapFile: beatmapFile,
                             accuracy: playmode is Playmode.Mania or Playmode.Taiko ? 1 : (double)score.Accuracy!,
                             scoreMaxCombo: null,
                             scoreMods: scoreMods,
                             scoreStatistics: scoreStatisticsIfFc,
                             rulesetId: (int)playmode,
                             cancellationToken: Context.CancellationToken)
                };
                _logger.LogInformation("[/last] End of calculating pp");
            }
            var scoreRank = _scoreHelper.GetScoreRankEmoji(score.Rank!, score.Passed!.Value) +
                            _scoreHelper.ParseScoreRank(score.Passed!.Value ? score.Rank! : "F");
            var counterText = lastScores.Length == 1 ? "" : $"{i + 1}. ";
            double? scorePp = calculatedPp?.Current?.Pp ?? score.Pp;
            if (scorePp is double.NaN) scorePp = null;

            double? scorePpIfFc = calculatedPp?.IfFC?.Pp;
            double? accuracyIfFc = calculatedPp?.IfFC?.CalculatedAccuracy;
            bool scoreModsContainsModIdk = scoreMods.Any(m => m is ModIdk);


            // Beatmap max combo from pp calculation (or use beatmap.MaxCombo if null)
            int? beatmapMaxCombo = calculatedPp?.IfFC?.BeatmapMaxCombo;
            if (beatmap.ModeInt == (int)playmode)
            {
                beatmapMaxCombo ??= beatmap.MaxCombo;
            }

            // Calculate diff rating
            double? difficultyRating = calculatedPp?.IfFC?.DifficultyAttributes.StarRating;
            if (difficultyRating == null)
            {
                var beatmapAttributesResponse = await _osuApiV2.Beatmaps.GetBeatmapAttributes(beatmap.Id.Value, new() { RulesetId = ((int)playmode).ToString(), Mods = mods });

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
                accuracyIfFc = (double)score.Accuracy!;
            }

            string scorePpText =
                _scoreHelper.GetFormattedNumConsideringNull(scoreModsContainsModIdk && calculatedPp?.Current != null
                    ? null
                    : scorePp);

            string scoreIfFcPpText =
                $"{_scoreHelper.GetFormattedNumConsideringNull(scoreModsContainsModIdk ? null : scorePpIfFc)}pp if {_scoreHelper.GetFormattedNumConsideringNull(accuracyIfFc * 100, round: false)}% FC";

            var scoreEndedMinutesAgoText =
                (DateTime.UtcNow - score.EndedAt!.Value).Humanize(
                    culture: CultureInfo.GetCultureInfoByIetfLanguageTag("ru-RU")) + " назад";

            // Caclculate completion
            if (playmode == Playmode.Catch)
            {
                calculatedPp?.IfFC?.ScoreHitResultsCount -= score.Statistics!.LargeTickHit;
            }
            double? completion = (double)score.CalculateSumOfHitResults(playmode) / calculatedPp?.IfFC?.ScoreHitResultsCount * 100.0;
            if (completion == null)
            {
                if (passed)
                {
                    completion = 100;
                }
                else
                {
                    completion = score.MaxCombo / beatmapMaxCombo;
                }
            }

            string globalRankText = "";
            if (score.RankGlobal != null && score.RankGlobal is > 0 and <= 10000)
            {
                globalRankText = $"<b>Global #{score.RankGlobal}</b>\n";
            }

            textToSend += language.command_last.Fill([
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
                $"{_scoreHelper.GetModsText(mods)}",
                $"{score.MaxCombo}",
                $"{_scoreHelper.GetFormattedNumConsideringNull(beatmapMaxCombo, format: "F0")}",
                $"{scorePpText}",
                $"{scoreIfFcPpText}",
                $"{_scoreHelper.GetScoreUrlWrappedInString(score.Id!.Value, "link")}",
                $"{scoreEndedMinutesAgoText}",
                $"{_scoreHelper.GetFormattedNumConsideringNull(completion, format: "N1")}"
            ]);

            if (scoreModsContainsModIdk)
                textToSend += "\nВ скоре присутствуют неизвестные боту моды, расчет пп невозможен.";

            if (beatmapContainsTooManyHitObjects)
                textToSend += "\nВ карте слишком много объектов, доступная информация будет ограничена.";

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