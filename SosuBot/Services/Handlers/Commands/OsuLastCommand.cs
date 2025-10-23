using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using OsuApi.V2;
using OsuApi.V2.Clients.Beatmaps.HttpIO;
using OsuApi.V2.Clients.Users.HttpIO;
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
    private bool _onlyPassed;
    private ApiV2 _osuApiV2 = null!;
    private ILogger<OsuLastCommand> _logger = null!;
    private ILogger<PPCalculator> _loggerPpCalculator = null!;

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

        var osuUsernameForLastScores = string.Empty;
        var parameters = Context.Update.Text!.GetCommandParameters()!;

        var limit = 1;
        string? ruleset = null;

        //l
        if (parameters.Length == 0)
        {
            if (osuUserInDatabase is null)
            {
                await waitMessage.EditAsync(Context.BotClient, language.error_userNotSetHimself);
                return;
            }

            osuUsernameForLastScores = osuUserInDatabase.OsuUsername;
            ruleset = osuUserInDatabase.OsuMode.ToRuleset();
        }
        //l 5
        //l mrekk
        else if (parameters.Length == 1)
        {
            var limitParsed = parameters[0].Length == 1 && int.TryParse(char.ToString(parameters[0][0]), out limit);
            var positionalParametersExists = parameters[0].StartsWith("mode=");
            if (limitParsed || positionalParametersExists)
            {
                if (osuUserInDatabase is null)
                {
                    await waitMessage.EditAsync(Context.BotClient, language.error_userNotSetHimself);
                    return;
                }

                osuUsernameForLastScores = osuUserInDatabase.OsuUsername;
                ruleset = osuUserInDatabase.OsuMode.ToRuleset();
            }

            if (positionalParametersExists)
            {
                ruleset = parameters[0].Split('=')[1].ParseToRuleset();
                if (ruleset is null)
                {
                    await waitMessage.EditAsync(Context.BotClient, language.error_modeIncorrect);
                    return;
                }
            }

            if (!limitParsed && !positionalParametersExists) osuUsernameForLastScores = parameters[0];
        }
        //l mrekk 5
        else if (parameters.Length == 2)
        {
            limit = int.Parse(Regex.Match(string.Join(" ", parameters), @"(\d)").Value);
            osuUsernameForLastScores = Regex.Match(string.Join(" ", parameters), @"(\S{3,})").Value;
        }
        else
        {
            await waitMessage.EditAsync(Context.BotClient, language.error_argsLength);
            return;
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

            var sum = beatmap.CountCircles + beatmap.CountSliders + beatmap.CountSpinners;
            if (sum is > 20_000)
            {
                await waitMessage.EditAsync(Context.BotClient, language.error_baseMessage + "\nСлишком большая карта!");
                return;
            }

            var mods = score.Mods!;

            if (i == 0) chatInDatabase!.LastBeatmapId = beatmap.Id;

            bool passed = score.Passed!.Value;
            Dictionary<HitResult, int> scoreStatistics = score.Statistics!.ToStatistics();

            Mod[] scoreMods = mods.ToOsuMods(playmode);
            var calculatedPp = new PPResult
            {
                Current = score.Pp != null
                    ? null
                    : await ppCalculator.CalculatePpAsync(beatmap.Id.Value, (double)score.Accuracy!,
                        scoreMaxCombo: score.MaxCombo!.Value,
                        passed: passed,
                        scoreMods: scoreMods,
                        scoreStatistics: scoreStatistics,
                        rulesetId: (int)playmode,
                        cancellationToken: Context.CancellationToken),

                IfFC = await ppCalculator.CalculatePpAsync(beatmap.Id.Value, (double)score.Accuracy!,
                    scoreMaxCombo: beatmap.MaxCombo!.Value,
                    scoreMods: scoreMods,
                    scoreStatistics: null,
                    rulesetId: (int)playmode,
                    greatsMania: playmode == Playmode.Mania ? score.Statistics?.Great : null,
                    cancellationToken: Context.CancellationToken)
            };
            
            string scoreRank = ScoreHelper.GetScoreRankEmoji(score.Rank!, score.Passed!.Value) +
                            ScoreHelper.ParseScoreRank(score.Passed!.Value ? score.Rank! : "F");
            string textBeforeBeatmapLink = lastScores.Length == 1 ? "" : $"{i + 1}. ";
            double? scorePp = calculatedPp.Current?.Pp ?? score.Pp!.Value;
            if (scorePp is Double.NaN)
            {
                scorePp = null;
            }
            
            double? scorePpIfFc = calculatedPp.IfFC.Pp;
            double accuracyIfFc = calculatedPp.IfFC.CalculatedAccuracy;
            bool isFc = score.MaxCombo == beatmap.MaxCombo;
            bool scoreModsContainsModIdk = scoreMods.Any(m => m is ModIdk);

            if (isFc)
            {
                scorePpIfFc = scorePp;
                accuracyIfFc = (double)score.Accuracy;
            }

            string scorePpText = ScoreHelper.GetFormattedPpTextConsideringNull(scoreModsContainsModIdk && calculatedPp.Current != null ? null : scorePp);
            string scoreIfFcPpText =
                ScoreHelper.GetFormattedPpTextConsideringNull(scoreModsContainsModIdk ? null : scorePpIfFc);

            int scoreEndedMinutesAgo = (int)(DateTime.UtcNow - score.EndedAt!.Value).TotalMinutes;

            textToSend += language.command_last.Fill([
                $"{textBeforeBeatmapLink}",
                $"{scoreRank}",
                $"{beatmap.Id}",
                $"{score.Beatmapset?.Title.EncodeHtml()}",
                $"{beatmap.Version.EncodeHtml()}",
                $"{beatmap.Status}",
                $"{ppCalculator.LastDifficultyAttributes!.StarRating:N2}",
                $"{ScoreHelper.GetScoreStatisticsText(score.Statistics!, playmode)}",
                $"{score.Statistics!.Miss}",
                $"{score.Accuracy * 100:N2}",
                $"{ScoreHelper.GetModsText(mods)}",
                $"{score.MaxCombo}",
                $"{beatmap.MaxCombo}",
                $"{scorePpText}",
                $"{scoreIfFcPpText}",
                $"{accuracyIfFc * 100:N2}",
                $"{scoreEndedMinutesAgo}",
                $"{score.CalculateCompletion(beatmap, playmode):N1}"
            ]);
            if (scoreModsContainsModIdk)
            {
                textToSend += "\nВ скоре присутствуют неизвестные боту моды, расчет пп невозможен.";
            }

            textToSend += "\n\n";
        }

        if (playmode != Playmode.Osu) textToSend += "Для не std-скоров расчет пп на FC может быть не верным.";

        await waitMessage.EditAsync(Context.BotClient, textToSend);
    }
}