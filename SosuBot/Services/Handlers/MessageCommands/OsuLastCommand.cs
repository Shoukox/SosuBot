using Microsoft.EntityFrameworkCore;
using osu.Game.Extensions;
using osu.Game.Rulesets.Osu.Mods;
using OsuApi.Core.V2;
using OsuApi.Core.V2.Beatmaps.Models.HttpIO;
using OsuApi.Core.V2.Scores.Models;
using OsuApi.Core.V2.Users.Models;
using OsuApi.Core.V2.Users.Models.HttpIO;
using PerfomanceCalculator;
using Sosu.Localization;
using SosuBot.Database;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.Marshalling;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace SosuBot.Services.Handlers.MessageCommands
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

            Score[]? lastScores = null;
            string? osuUsernameForLastScores = null;
            bool osuUsernameWasTakenFromParameters = true;
            string[] parameters = Context.Text.GetCommandParameters()!;

            int limit = 1;
            string ruleset = Ruleset.Osu;

            // l
            // l 5, not set
            if (parameters.Length == 0 || parameters[0].Length == 1)
            {
                if (osuUserInDatabase is null)
                {
                    await waitMessage.EditAsync(BotClient, language.error_noUser);
                    return;
                }
                else
                {
                    osuUsernameWasTakenFromParameters = false;
                    osuUsernameForLastScores = osuUserInDatabase.OsuUsername;
                    ruleset = osuUserInDatabase.OsuMode;
                }
            }

            // l 5, set
            // l Shoukko
            if (parameters.Length == 1)
            {
                if (parameters[0].Length == 1) limit = int.Parse(parameters[0]);
                else osuUsernameForLastScores = parameters[0];
            }

            // l mrekk 5
            if (parameters.Length == 2)
            {
                osuUsernameForLastScores = parameters[0];
                limit = int.Parse(parameters[1]);
            }

            var userResponse = await OsuApiV2.Users.GetUser($"@{osuUsernameForLastScores}", new());
            if (userResponse is null)
            {
                await waitMessage.EditAsync(BotClient, language.error_userNotFound + "\n\n" + language.error_hintReplaceSpaces);
                return;
            }

            if (osuUsernameWasTakenFromParameters || osuUserInDatabase is null)
            {
                ruleset = userResponse.UserExtend!.Playmode!;
            }

            var lastScoresResponse = await OsuApiV2.Users.GetUserScores(userResponse.UserExtend!.Id.Value, ScoreType.Recent, new() { IncludeFails = "1", Limit = limit, Mode = ruleset });
            if (lastScoresResponse!.Scores.Length == 0)
            {
                await waitMessage.EditAsync(BotClient, language.error_noPreviousScores);
                return;
            }

            lastScores = lastScoresResponse.Scores;
            GetBeatmapResponse[] beatmaps = lastScores.Select(async score => await OsuApiV2.Beatmaps.GetBeatmap((long)score.Beatmap!.Id)).Select(t => t.Result).ToArray()!;
            var ppCalculator = new PPCalculator();

            string textToSend = $"<b>{osuUsernameForLastScores}</b>\n\n";
            for (int i = 0; i <= lastScores.Length - 1; i++)
            {
                var score = lastScores[i];
                var beatmap = beatmaps[i].BeatmapExtended!;
                Mod[] mods = score.Mods!;

                var calculatedPP = new
                {
                    Current = await ppCalculator.CalculatePPAsync(
                        beatmap.Id.Value,
                        score.Statistics!.Great,
                        score.Statistics!.Ok,
                        score.Statistics!.Meh,
                        score.Statistics!.Miss,
                        score.MaxCombo!.Value,
                        mods.ToOsuMods()),
                    NoMiss = await ppCalculator.CalculatePPAsync(
                        beatmap.Id.Value,
                        score.Statistics!.Great,
                        score.Statistics!.Ok,
                        score.Statistics!.Meh,
                        0,
                        beatmap.MaxCombo!.Value,
                        mods.ToOsuMods()),
                };

                string modsText = "+" + string.Join("", score.Mods!.Select(m => m.Acronym));
                if (modsText == "+") modsText += "NM";

                textToSend += language.command_last.Fill([
                    $"{i + 1}",
                    $"{score.Rank}",
                    $"{beatmap.Id}",
                    $"{score.Beatmapset!.Title}",
                    $"{beatmap.Version}",
                    $"{beatmap.Status}",
                    $"{score.Statistics!.Great}",
                    $"{score.Statistics!.Ok}",
                    $"{score.Statistics!.Meh}",
                    $"{score.Statistics!.Miss}",
                    $"{score.Accuracy*100:N2}",
                    $"{modsText}",
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
