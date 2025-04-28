using Microsoft.EntityFrameworkCore.Diagnostics;
using MongoDB.Bson.Serialization.Serializers;
using osu.Game.Overlays.Settings.Sections.Gameplay;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Osu.Skinning.Argon;
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
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace SosuBot.Services.Handlers.MessageCommands
{
    public class OsuScoreCommand : CommandBase<Message>
    {
        public static string[] Commands = ["/score", "/s"];

        public override async Task ExecuteAsync()
        {
            ILocalization language = new Russian();
            TelegramChat? chatInDatabase = await Database.TelegramChats.FindAsync(Context.Chat.Id);
            OsuUser? osuUserInDatabase = await Database.OsuUsers.FindAsync(Context.From!.Id);

            Message waitMessage = await Context.ReplyAsync(BotClient, language.waiting);

            Score[] scores;
            Playmode? playmode;
            int? beatmapId = null;
            string osuUsernameForScore = string.Empty;
            string[] parameters = Context.Text!.GetCommandParameters()!;

            // s
            if (parameters.Length == 0)
            {
                if (OsuHelper.ParseOsuBeatmapLink(Context.ReplyToMessage?.Text, out int? beatmapsetId, out beatmapId) is string link)
                {
                    if (beatmapId is null && beatmapsetId is not null)
                    {
                        BeatmapsetExtended beatmapset = await OsuApiV2.Beatmapsets.GetBeatmapset(beatmapsetId.Value);
                        beatmapId = beatmapset.Beatmaps![0].Id!;
                    }
                }
                else
                {
                    beatmapId = chatInDatabase!.LastBeatmapId;
                }

                if (osuUserInDatabase is null)
                {
                    await waitMessage.EditAsync(BotClient, language.error_noUser);
                    return;
                }
                playmode = osuUserInDatabase.OsuMode;
                osuUsernameForScore = osuUserInDatabase.OsuUsername;
            }
            // s url
            // s mrekk
            // s mrekk, with reply
            else if (parameters.Length == 1)
            {
                // s mrekk, with reply
                int? beatmapsetId = null;
                string? link = OsuHelper.ParseOsuBeatmapLink(Context.ReplyToMessage?.Text, out beatmapsetId, out beatmapId);
                if (link is not null)
                {
                    if (beatmapId is null && beatmapsetId is not null)
                    {
                        BeatmapsetExtended beatmapset = await OsuApiV2.Beatmapsets.GetBeatmapset(beatmapsetId.Value);
                        beatmapId = beatmapset.Beatmaps![0].Id!;
                    }

                    osuUsernameForScore = parameters[0];
                    playmode = null;
                }
                // s url
                else
                {
                    link ??= OsuHelper.ParseOsuBeatmapLink(parameters[0], out beatmapsetId, out beatmapId);
                    if (link is not null)
                    {
                        if (beatmapId is null && beatmapsetId is not null)
                        {
                            BeatmapsetExtended beatmapset = await OsuApiV2.Beatmapsets.GetBeatmapset(beatmapsetId.Value);
                            beatmapId = beatmapset.Beatmaps![0].Id!;
                        }

                        if (osuUserInDatabase is null)
                        {
                            await waitMessage.EditAsync(BotClient, language.error_noUser);
                            return;
                        }
                        osuUsernameForScore = osuUserInDatabase.OsuUsername;
                        playmode = osuUserInDatabase.OsuMode;
                    }
                    // s mrekk
                    else
                    {
                        osuUsernameForScore = parameters[0];
                        playmode = null;
                        beatmapId = chatInDatabase!.LastBeatmapId;
                    }
                }
            }
            // s mrekk url
            // s url mrekk
            else if (parameters.Length == 2)
            {
                if (OsuHelper.ParseOsuBeatmapLink(Context.Text, out int? beatmapsetId, out beatmapId) is { } link)
                {
                    if (beatmapId is null && beatmapsetId is not null)
                    {
                        BeatmapsetExtended beatmapset = await OsuApiV2.Beatmapsets.GetBeatmapset(beatmapsetId.Value);
                        beatmapId = beatmapset.Beatmaps![0].Id!;
                    }

                    if (parameters[0] == link.Trim())
                    {
                        osuUsernameForScore = parameters[1];
                    }
                    else
                    {
                        osuUsernameForScore = parameters[0];
                    }
                }
                playmode = null;
            }
            else
            {
                await waitMessage.EditAsync(BotClient, language.error_argsLength);
                return;
            }

            if (beatmapId is null)
            {
                await waitMessage.EditAsync(BotClient, language.error_baseMessage);
                return;
            }

            // getting osu!player through username
            var userResponse = await OsuApiV2.Users.GetUser($"@{osuUsernameForScore}", new());
            if (userResponse is null)
            {
                await waitMessage.EditAsync(BotClient, language.error_userNotFound + "\n\n" + language.error_hintReplaceSpaces);
                return;
            }

            // if username was entered, then use as ruleset his (this username) standard ruleset.
            playmode ??= userResponse.UserExtend!.Playmode!.ParseRulesetToPlaymode();

            var scoresResponse = await OsuApiV2.Beatmaps.GetUserBeatmapScores(beatmapId.Value, userResponse.UserExtend!.Id.Value, new() { Ruleset = playmode.Value.ToRuleset()});
            if (scoresResponse is null)
            {
                await waitMessage.EditAsync(BotClient, language.error_noRecords);
                return;
            }
            scores = scoresResponse.Scores!;
            if (scores.Length == 0)
            {
                await waitMessage.EditAsync(BotClient, language.error_noRecords);
                return;
            }

            string textToSend = $"<b>{osuUsernameForScore}</b>\n\n";
            for (int i = 0; i <= scores.Length - 1; i++)
            {
                var score = scores[i];
                var beatmap = (await OsuApiV2.Beatmaps.GetBeatmap(scores[i].BeatmapId!.Value))!.BeatmapExtended;
                var beatmapset = await OsuApiV2.Beatmapsets.GetBeatmapset(beatmap!.BeatmapsetId.Value);

                Mod[] mods = score.Mods!;
                double accuracy = score.Accuracy!.Value;

                if (i == 0)
                {
                    chatInDatabase!.LastBeatmapId = beatmap!.Id;
                }

                double curpp = score.Pp!.Value;
                textToSend += language.command_score.Fill([
                    $"{score.Rank}",
                    $"{beatmap.Url}",
                    $"{beatmapset.Title}",
                    $"{beatmap.Version}",
                    $"{beatmap.Status}",
                    $"{ScoreHelper.GetScoreStatisticsText(score.Statistics!, playmode.Value)}",
                    $"{score.Statistics!.Miss}",
                    $"{score.Accuracy*100:N2}",
                    $"{ScoreHelper.GetModsText(score.Mods!)}",
                    $"{score.MaxCombo}",
                    $"{beatmap.MaxCombo}",
                    $"{curpp:N2}",
                    $"{score.EndedAt!.Value:dd.MM.yyyy HH:mm zzz}"]);
            }
            await waitMessage.EditAsync(BotClient, textToSend);
        }
    }
}
