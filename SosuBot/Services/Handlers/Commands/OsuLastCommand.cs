using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using Humanizer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using OsuApi.V2;
using OsuApi.V2.Clients.Beatmaps.HttpIO;
using OsuApi.V2.Clients.Users.HttpIO;
using OsuApi.V2.Models;
using OsuApi.V2.Users.Models;
using SosuBot.Extensions;
using SosuBot.Helpers;
using SosuBot.Helpers.OutputText;
using SosuBot.Helpers.Types;
using SosuBot.Localization;
using SosuBot.Localization.Languages;
using SosuBot.PerformanceCalculator;
using SosuBot.Services.Handlers.Abstract;
using Telegram.Bot.Types;

namespace SosuBot.Services.Handlers.Commands;

public class OsuLastCommand(bool onlyPassed = false) : CommandBase<Message>
{
    public static readonly string[] Commands = ["/last", "/l"];
    private ILogger<OsuLastCommand> _logger = null!;
    private ILogger<PPCalculator> _loggerPpCalculator = null!;
    private bool _onlyPassed;
    private ApiV2 _osuApiV2 = null!;

    public override Task BeforeExecuteAsync()
    {
        _onlyPassed = onlyPassed;
        _osuApiV2 = Context.ServiceProvider.GetRequiredService<ApiV2>();
        _logger = Context.ServiceProvider.GetRequiredService<ILogger<OsuLastCommand>>();
        _loggerPpCalculator = Context.ServiceProvider.GetRequiredService<ILogger<PPCalculator>>();
        return Task.CompletedTask;
    }

    public override async Task ExecuteAsync()
    {
        await BeforeExecuteAsync();

        if (await Context.Update.IsUserSpamming(Context.BotClient))
            return;

        ILocalization language = new Russian();
        var chatInDatabase = await Context.Database.TelegramChats.FindAsync(Context.Update.Chat.Id);
        var osuUserInDatabase = await Context.Database.OsuUsers.FindAsync(Context.Update.From!.Id);

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
            if(!int.TryParse(numberAsText, out limit))
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

        if (ruleset == null && keywordParameters.Length != 0)
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

        var lastScoresResponse = await _osuApiV2.Users.GetUserScores(userResponse.UserExtend!.Id.Value,
            ScoreType.Recent,
            new GetUserScoreQueryParameters
            { IncludeFails = Convert.ToInt32(!_onlyPassed), Limit = limit, Mode = ruleset });
        if (lastScoresResponse!.Scores.Length == 0)
        {
            await waitMessage.EditAsync(Context.BotClient,
                language.error_noPreviousScores.Fill([ruleset.ParseRulesetToGamemode()]));
            return;
        }

        var lastScores = lastScoresResponse.Scores;
        GetBeatmapResponse[] beatmaps = lastScores
            .Select(async score => await _osuApiV2.Beatmaps.GetBeatmap((long)score.Beatmap!.Id))
            .Select(t => t.Result).ToArray()!;
        var ppCalculator = new PPCalculator(_loggerPpCalculator);

        var textToSend =
            $"<b>{UserHelper.GetUserProfileUrlWrappedInUsernameString(userResponse.UserExtend!.Id.Value, osuUsernameForLastScores)}</b> (<i>{ruleset.ParseRulesetToGamemode()}</i>)\n\n";

        var playmode = (Playmode)lastScores[0].RulesetId!;
        for (var i = 0; i <= lastScores.Length - 1; i++)
        {
            var score = lastScores[i];
            var beatmap = beatmaps[i].BeatmapExtended!;

            var hitobjectsSum = beatmap.CountCircles + beatmap.CountSliders + beatmap.CountSpinners;
            bool beatmapContainsTooManyHitObjects = hitobjectsSum >= 20000;
            var mods = score.Mods!;

            if (i == 0) chatInDatabase!.LastBeatmapId = beatmap.Id;

            var passed = score.Passed!.Value;
            var scoreStatistics = score.Statistics!.ToStatistics();

            // Get osu! mods of the score
            var scoreMods = mods.ToOsuMods(playmode);

            // Get score statistics for fc
            Dictionary<HitResult, int>? scoreStatisticsIfFc = null;
            if (passed && playmode != Playmode.Mania)
            {
                scoreStatisticsIfFc = new Dictionary<HitResult, int>()
                {
                    {
                        HitResult.Great,
                        scoreStatistics.GetValueOrDefault(HitResult.Great) +
                        playmode == Playmode.Catch ? scoreStatistics.GetValueOrDefault(HitResult.Miss) : 0
                    },
                    { HitResult.Good, scoreStatistics.GetValueOrDefault(HitResult.Good) },
                    { HitResult.Ok, scoreStatistics.GetValueOrDefault(HitResult.Ok) },
                    {
                        HitResult.Meh,
                        scoreStatistics.GetValueOrDefault(HitResult.Meh) +
                        playmode != Playmode.Catch ? scoreStatistics.GetValueOrDefault(HitResult.Miss) : 0
                    },
                    { HitResult.Miss, 0 }
                };
            }

            // Calculate pp
            PPResult? calculatedPp = new PPResult() { Current = null, IfFC = null };
            if (!beatmapContainsTooManyHitObjects)
            {
                calculatedPp = new PPResult
                {
                    Current = score.Pp != null ? null : 
                        await ppCalculator.CalculatePpAsync(beatmap.Id.Value, (double)score.Accuracy!,
                             scoreMaxCombo: score.MaxCombo!.Value,
                             passed: passed,
                             scoreMods: scoreMods,
                             scoreStatistics: scoreStatistics,
                             rulesetId: (int)playmode,
                             cancellationToken: Context.CancellationToken),

                    IfFC = await ppCalculator.CalculatePpAsync(beatmap.Id.Value,
                             playmode == Playmode.Mania ? 1 : (double)score.Accuracy!,
                             scoreMaxCombo: null,
                             scoreMods: scoreMods,
                             scoreStatistics: scoreStatisticsIfFc,
                             rulesetId: (int)playmode,
                             cancellationToken: Context.CancellationToken)
                };
            }
            var scoreRank = ScoreHelper.GetScoreRankEmoji(score.Rank!, score.Passed!.Value) +
                            ScoreHelper.ParseScoreRank(score.Passed!.Value ? score.Rank! : "F");
            var counterText = lastScores.Length == 1 ? "" : $"{i + 1}. ";
            double? scorePp = calculatedPp?.Current?.Pp ?? score.Pp;
            if (scorePp is double.NaN) scorePp = null;

            double? scorePpIfFc = calculatedPp?.IfFC?.Pp;
            double? accuracyIfFc = calculatedPp?.IfFC?.CalculatedAccuracy;
            bool scoreModsContainsModIdk = scoreMods.Any(m => m is ModIdk);
            

            // Beatmap max combo from pp calculation (or use beatmap.MaxCombo if null)
            int? beatmapMaxCombo = calculatedPp?.IfFC?.BeatmapMaxCombo;
            if(beatmap.ModeInt == (int)playmode)
            {
                beatmapMaxCombo ??= beatmap.MaxCombo;
            }

            // Calculate diff rating
            double? difficultyRating = ppCalculator.LastDifficultyAttributes?.StarRating;
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
                ScoreHelper.GetFormattedNumConsideringNull(scoreModsContainsModIdk && calculatedPp?.Current != null
                    ? null
                    : scorePp);

            string scoreIfFcPpText =
                $"{ScoreHelper.GetFormattedNumConsideringNull(scoreModsContainsModIdk ? null : scorePpIfFc)}pp if {ScoreHelper.GetFormattedNumConsideringNull(accuracyIfFc * 100, round: false)}% FC";

            var scoreEndedMinutesAgoText =
                (DateTime.UtcNow - score.EndedAt!.Value).Humanize(
                    culture: CultureInfo.GetCultureInfoByIetfLanguageTag("ru-RU")) + " назад";

            // Caclculate completion
            double? completion = (double)score.CalculateSumOfHitResults() / calculatedPp?.IfFC?.ScoreHitResultsCount * 100.0;
            if(completion == null)
            {
                if (passed) completion = 100;
                else
                {
                    completion = score.MaxCombo / beatmapMaxCombo;
                }
            }

            textToSend += language.command_last.Fill([
                $"{counterText}",
                $"{scoreRank}",
                $"{beatmap.Id}",
                $"{score.Beatmapset?.Title.EncodeHtml()}",
                $"{beatmap.Version.EncodeHtml()}",
                $"{beatmap.Status}",
                $"{ScoreHelper.GetFormattedNumConsideringNull(difficultyRating, format: "N2", round: false)}",
                $"{ScoreHelper.GetScoreStatisticsText(score.Statistics!, playmode)}",
                $"{score.Statistics!.Miss}",
                $"{ScoreHelper.GetFormattedNumConsideringNull(score.Accuracy * 100, round: false)}",
                $"{ScoreHelper.GetModsText(mods)}",
                $"{score.MaxCombo}",
                $"{ScoreHelper.GetFormattedNumConsideringNull(beatmapMaxCombo, format: "F0")}",
                $"{scorePpText}",
                $"{scoreIfFcPpText}",
                $"{scoreEndedMinutesAgoText}",
                $"{ScoreHelper.GetFormattedNumConsideringNull(completion, format: "N1")}"
            ]);

            if (scoreModsContainsModIdk)
                textToSend += "\nВ скоре присутствуют неизвестные боту моды, расчет пп невозможен.";

            if (beatmapContainsTooManyHitObjects)
                textToSend += "\nВ карте слишком много объектов, доступная информация будет ограничена.";

            textToSend += "\n\n";
        }

        await waitMessage.EditAsync(Context.BotClient, textToSend);
    }
}