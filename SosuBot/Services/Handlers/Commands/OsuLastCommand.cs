using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using OsuApi.V2.Clients.Beatmaps.HttpIO;
using OsuApi.V2.Clients.Users.HttpIO;
using OsuApi.V2.Models;
using OsuApi.V2.Users.Models;
using SosuBot.Extensions;
using SosuBot.Helpers.OutputText;
using SosuBot.Helpers.Types;
using SosuBot.Localization;
using SosuBot.Localization.Languages;
using SosuBot.Logging;
using SosuBot.PerformanceCalculator;
using SosuBot.Services.Handlers.Abstract;
using Telegram.Bot.Types;

namespace SosuBot.Services.Handlers.Commands;

public class OsuLastCommand(bool onlyPassed = false) : CommandBase<Message>
{
    private static readonly ILogger Logger = ApplicationLogging.CreateLogger(nameof(OsuLastCommand));
    public static readonly string[] Commands = ["/last", "/l"];

    public override async Task ExecuteAsync()
    {
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
            await Context.OsuApiV2.Users.GetUser($"@{osuUsernameForLastScores}", new GetUserQueryParameters());
        if (userResponse is null)
        {
            await waitMessage.EditAsync(Context.BotClient,
                language.error_userNotFound + "\n\n" + language.error_hintReplaceSpaces);
            return;
        }

        osuUsernameForLastScores = userResponse.UserExtend!.Username!;

        // if username was entered, then use as ruleset his (this username) standard ruleset.
        ruleset ??= userResponse.UserExtend!.Playmode!;

        var lastScoresResponse = await Context.OsuApiV2.Users.GetUserScores(userResponse.UserExtend!.Id.Value,
            ScoreType.Recent, new GetUserScoreQueryParameters { IncludeFails = Convert.ToInt32(!onlyPassed), Limit = limit, Mode = ruleset });
        if (lastScoresResponse!.Scores.Length == 0)
        {
            await waitMessage.EditAsync(Context.BotClient,
                language.error_noPreviousScores.Fill([ruleset.ParseRulesetToGamemode()]));
            return;
        }

        Score[] lastScores = lastScoresResponse.Scores;
        GetBeatmapResponse[] beatmaps = lastScores
            .Select(async score => await Context.OsuApiV2.Beatmaps.GetBeatmap((long)score.Beatmap!.Id))
            .Select(t => t.Result).ToArray()!;
        var ppCalculator = new PPCalculator();

        var textToSend =
            $"<b>{UserHelper.GetUserProfileUrlWrappedInUsernameString(userResponse.UserExtend!.Id.Value, osuUsernameForLastScores)}</b> (<i>{ruleset.ParseRulesetToGamemode()}</i>)\n\n";
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
            var playmode = (Playmode)score.RulesetId!;

            if (i == 0) chatInDatabase!.LastBeatmapId = beatmap.Id;

            bool passed = score.Passed!.Value;
            var scoreStatistics = score.Statistics!.ToStatistics();

            var calculatedPp = new PPResult
            {
                Current = score.Pp != null ? null : await ppCalculator.CalculatePpAsync(beatmap.Id.Value, (double)score.Accuracy!,
                    scoreMaxCombo: score.MaxCombo!.Value,
                    passed: passed,
                    scoreMods: mods.ToOsuMods(playmode),
                    scoreStatistics: scoreStatistics,
                    rulesetId: (int)playmode,
                    cancellationToken: Context.CancellationToken),

                IfFC = await ppCalculator.CalculatePpAsync(beatmap.Id.Value, (double)score.Accuracy!,
                    scoreMaxCombo: beatmap.MaxCombo!.Value,
                    scoreMods: mods.ToOsuMods(playmode),
                    scoreStatistics: null,
                    rulesetId: (int)playmode,
                    cancellationToken: Context.CancellationToken),
            };

            var scoreRank = ScoreHelper.GetScoreRankEmoji(score.Rank!, score.Passed!.Value) + (score.Passed!.Value ? score.Rank! : "F");
            var textBeforeBeatmapLink = lastScores.Length == 1 ? "" : $"{i + 1}. ";
            double scorePp = calculatedPp.Current?.Pp ?? score.Pp!.Value;
            double scorePpIfFc = calculatedPp.IfFC.Pp;
            double accuracyIfFc = calculatedPp.IfFC.CalculatedAccuracy;
            bool isFc = score.MaxCombo == beatmap.MaxCombo;

            if (isFc)
            {
                Logger.LogWarning($"PP Calculation for fc and the gotten pp from the api do not match: scoreId={score.Id} calculatedPpForFC={calculatedPp.IfFC.Pp} gottenPp:{score.Pp}");
                scorePpIfFc = scorePp;
                accuracyIfFc = (double)score.Accuracy;
            }

            textToSend += language.command_last.Fill([
                $"{textBeforeBeatmapLink}",
                $"{scoreRank}",
                $"{beatmap.Id}",
                $"{score.Beatmapset?.Title.EncodeHtml()}",
                $"{beatmap.Version.EncodeHtml()}",
                $"{beatmap.Status}",
                $"{ppCalculator.LastDifficultyAttributes!.StarRating:N2}",
                $"{ScoreHelper.GetScoreStatisticsText(score.Statistics!, playmode)}",
                $"{score.Statistics!.Miss + score.Statistics!.LargeTickMiss}",
                $"{score.Accuracy * 100:N2}",
                $"{ScoreHelper.GetModsText(mods)}",
                $"{score.MaxCombo}",
                $"{beatmap.MaxCombo}",
                $"{scorePp:N2}",
                $"{scorePpIfFc:N2}",
                $"{accuracyIfFc*100:N2}",
                $"{score.EndedAt!.Value:dd.MM.yyyy HH:mm zz}",
                $"{score.CalculateCompletion(beatmap.CalculateObjectsAmount()):N1}"
            ]);
        }

        await waitMessage.EditAsync(Context.BotClient, textToSend);
    }
}