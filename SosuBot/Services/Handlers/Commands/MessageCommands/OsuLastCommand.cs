using osu.Game.Rulesets.Scoring;
using OsuApi.Core.V2.Beatmaps.Models.HttpIO;
using OsuApi.Core.V2.Scores.Models;
using OsuApi.Core.V2.Users.Models;
using PerfomanceCalculator;
using Sosu.Localization;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.Helpers;
using SosuBot.OsuTypes;
using System.Text.RegularExpressions;
using Telegram.Bot.Types;

namespace SosuBot.Services.Handlers.Commands.MessageCommands
{
    public class OsuLastCommand : CommandBase<Message>
    {
        public static string[] Commands = ["/last", "/l"];

        public override async Task ExecuteAsync()
        {
            ILocalization language = new Russian();
            TelegramChat? chatInDatabase = await Database.TelegramChats.FindAsync(Context.Chat.Id);
            OsuUser? osuUserInDatabase = await Database.OsuUsers.FindAsync(Context.From!.Id);

            Message waitMessage = await Context.ReplyAsync(BotClient, language.waiting);

            Score[] lastScores;
            string osuUsernameForLastScores = string.Empty;
            string[] parameters = Context.Text!.GetCommandParameters()!;

            int limit = 1;
            string? ruleset = null;

            //l
            if (parameters.Length == 0)
            {
                if (osuUserInDatabase is null)
                {
                    await waitMessage.EditAsync(BotClient, language.error_noUser);
                    return;
                }
                osuUsernameForLastScores = osuUserInDatabase.OsuUsername;
                ruleset = osuUserInDatabase.OsuMode.ToRuleset();
            }
            //l 5
            //l mrekk
            else if (parameters.Length == 1)
            {
                bool limitParsed = parameters[0].Length == 1 && int.TryParse(char.ToString(parameters[0][0]), out limit);
                bool positionalParametersExists = parameters[0].StartsWith("mode=");
                if (limitParsed || positionalParametersExists)
                {
                    if (osuUserInDatabase is null)
                    {
                        await waitMessage.EditAsync(BotClient, language.error_noUser);
                        return;
                    }
                    osuUsernameForLastScores = osuUserInDatabase.OsuUsername;
                }

                if (positionalParametersExists)
                {
                    ruleset = parameters[0].Split('=')[1].ParseToRuleset();
                    if (ruleset is null)
                    {
                        await waitMessage.EditAsync(BotClient, language.error_modeIncorrect);
                        return;
                    }
                }

                if (!limitParsed && !positionalParametersExists)
                {
                    osuUsernameForLastScores = parameters[0];
                }
            }
            //l mrekk 5
            else if (parameters.Length == 2)
            {
                limit = int.Parse(Regex.Match(string.Join(" ", parameters), @"(\d)").Value);
                osuUsernameForLastScores = Regex.Match(string.Join(" ", parameters), @"(\S{3,})").Value;
            }
            else
            {
                await waitMessage.EditAsync(BotClient, language.error_argsLength);
                return;
            }

            // getting osu!player through username
            var userResponse = await OsuApiV2.Users.GetUser($"@{osuUsernameForLastScores}", new());
            if (userResponse is null)
            {
                await waitMessage.EditAsync(BotClient, language.error_userNotFound + "\n\n" + language.error_hintReplaceSpaces);
                return;
            }

            // if username was entered, then use as ruleset his (this username) standard ruleset.
            ruleset ??= userResponse.UserExtend!.Playmode!;

            var lastScoresResponse = await OsuApiV2.Users.GetUserScores(userResponse.UserExtend!.Id.Value, ScoreType.Recent, new() { IncludeFails = 1, Limit = limit, Mode = ruleset });
            if (lastScoresResponse!.Scores.Length == 0)
            {
                await waitMessage.EditAsync(BotClient, language.error_noPreviousScores.Fill([ruleset.ParseRulesetToGamemode()]));
                return;
            }

            lastScores = lastScoresResponse.Scores;
            GetBeatmapResponse[] beatmaps = lastScores.Select(async score => await OsuApiV2.Beatmaps.GetBeatmap((long)score.Beatmap!.Id)).Select(t => t.Result).ToArray()!;
            var ppCalculator = new PPCalculator();

            string textToSend = $"<b>{osuUsernameForLastScores}</b> (<i>{ruleset.ParseRulesetToGamemode()}</i>)\n\n";
            for (int i = 0; i <= lastScores.Length - 1; i++)
            {
                var score = lastScores[i];
                var beatmap = beatmaps[i].BeatmapExtended!;
                Mod[] mods = score.Mods!;
                Playmode playmode = (Playmode)score.RulesetId!;

                if (i == 0)
                {
                    chatInDatabase!.LastBeatmapId = beatmap!.Id;
                }

                Dictionary<HitResult, int> currentStatistics = score.Statistics!.ToStatistics();
                Dictionary<HitResult, int> maximumStatistics = score.MaximumStatistics!.ToStatistics();

                // calculate pp
                var calculatedPP = new
                {
                    Current = score.Pp ?? await ppCalculator.CalculatePPAsync(
                        beatmap.Id.Value,
                        score.MaxCombo!.Value,
                        mods.ToOsuMods(playmode),
                        statistics: currentStatistics,
                        maxStatistics: maximumStatistics,
                        rulesetId: (int)playmode),

                    NoMiss = await ppCalculator.CalculatePPAsync(
                        beatmap.Id.Value,
                        beatmap.MaxCombo!.Value,
                        mods.ToOsuMods(playmode),
                        statistics: maximumStatistics,
                        maxStatistics: maximumStatistics,
                        rulesetId: (int)playmode)
                };

                textToSend += language.command_last.Fill([
                    $"{i + 1}",
                    $"{score.Rank}",
                    $"{beatmap.Id}",
                    $"{score.Beatmapset!.Title.EncodeHTML()}",
                    $"{beatmap.Version.EncodeHTML()}",
                    $"{beatmap.Status}",
                    $"{ScoreHelper.GetScoreStatisticsText(score.Statistics!, playmode)}",
                    $"{score.Statistics!.Miss + score.Statistics!.LargeTickMiss}",
                    $"{score.Accuracy*100:N2}",
                    $"{ScoreHelper.GetModsText(mods)}",
                    $"{score.MaxCombo}",
                    $"{beatmap.MaxCombo}",
                    $"{calculatedPP.Current:N2}",
                    $"{calculatedPP.NoMiss:N2}",
                    $"{score.EndedAt!.Value:dd.MM.yyyy HH:mm zzz}",
                    $"{score.CalculateCompletion(beatmap.CalculateObjectsAmount()):N1}"]);
            }
            await waitMessage.EditAsync(BotClient, textToSend);
        }
    }
}
