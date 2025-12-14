using Microsoft.Extensions.DependencyInjection;
using OsuApi.V2;
using OsuApi.V2.Clients.Beatmaps.HttpIO;
using OsuApi.V2.Clients.Users.HttpIO;
using OsuApi.V2.Users.Models;
using SosuBot.Extensions;
using SosuBot.Helpers.OutputText;
using SosuBot.Helpers.Types;
using SosuBot.Localization;
using SosuBot.Localization.Languages;
using SosuBot.Services.Handlers.Abstract;
using Telegram.Bot.Types;

namespace SosuBot.Services.Handlers.Commands;

public sealed class OsuScoreCommand : CommandBase<Message>
{
    public static readonly string[] Commands = ["/score", "/s"];
    private ApiV2 _osuApiV2 = null!;

    public override Task BeforeExecuteAsync()
    {
        _osuApiV2 = Context.ServiceProvider.GetRequiredService<ApiV2>();
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

        BeatmapsetExtended? beatmapset = null;
        Playmode? playmode;
        int? beatmapId;
        var osuUsernameForScore = string.Empty;
        var parameters = Context.Update.Text!.GetCommandParameters()!;

        // s
        if (parameters.Length == 0)
        {
            if (OsuHelper.ParseOsuBeatmapLink(Context.Update.ReplyToMessage?.GetAllLinks(), out var beatmapsetId,
                    out beatmapId) is not null)
            {
                if (beatmapId is null && beatmapsetId is not null)
                {
                    beatmapset = await _osuApiV2.Beatmapsets.GetBeatmapset(beatmapsetId.Value);
                    beatmapId = beatmapset.Beatmaps![0].Id;
                }
            }
            else
            {
                beatmapId = chatInDatabase!.LastBeatmapId;
            }

            if (osuUserInDatabase is null)
            {
                await waitMessage.EditAsync(Context.BotClient, language.error_userNotSetHimself);
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
            // s with reply
            var link = OsuHelper.ParseOsuBeatmapLink(Context.Update.ReplyToMessage?.GetAllLinks(), out var beatmapsetId,
                out beatmapId);
            if (link is not null)
            {
                if (beatmapId is null && beatmapsetId is not null)
                {
                    beatmapset = await _osuApiV2.Beatmapsets.GetBeatmapset(beatmapsetId.Value);
                    beatmapId = beatmapset.Beatmaps![0].Id;
                }

                osuUsernameForScore = parameters[0];
                playmode = null;
            }
            else
            {
                // s url
                link = OsuHelper.ParseOsuBeatmapLink([parameters[0]], out beatmapsetId, out beatmapId);
                if (link is not null)
                {
                    if (beatmapId is null && beatmapsetId is not null)
                    {
                        beatmapset = await _osuApiV2.Beatmapsets.GetBeatmapset(beatmapsetId.Value);
                        beatmapId = beatmapset.Beatmaps![0].Id;
                    }

                    if (osuUserInDatabase is null)
                    {
                        await waitMessage.EditAsync(Context.BotClient, language.error_userNotSetHimself);
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
            if (OsuHelper.ParseOsuBeatmapLink(Context.Update.GetAllLinks(), out var beatmapsetId, out beatmapId) is
                { } link)
            {
                if (beatmapId is null && beatmapsetId is not null)
                {
                    beatmapset = await _osuApiV2.Beatmapsets.GetBeatmapset(beatmapsetId.Value);
                    beatmapId = beatmapset.Beatmaps![0].Id;
                }

                if (parameters[0] == link.Trim())
                    osuUsernameForScore = parameters[1];
                else
                    osuUsernameForScore = parameters[0];
            }

            playmode = null;
        }
        else
        {
            await waitMessage.EditAsync(Context.BotClient, language.error_argsLength);
            return;
        }

        if (beatmapId is null)
        {
            await waitMessage.EditAsync(Context.BotClient, language.error_baseMessage);
            return;
        }

        // getting osu!player through username
        var userResponse =
            await _osuApiV2.Users.GetUser($"@{osuUsernameForScore}", new GetUserQueryParameters());
        if (userResponse is null)
        {
            await waitMessage.EditAsync(Context.BotClient,
                language.error_userNotFound + "\n\n" + language.error_hintReplaceSpaces);
            return;
        }

        osuUsernameForScore = userResponse.UserExtend!.Username;

        // if username was entered, then use as ruleset his (this username) standard ruleset.
        playmode ??= userResponse.UserExtend!.Playmode!.ParseRulesetToPlaymode();
        var areScoresFromOtherRuleset = false;
        Playmode? beatmapPlaymode = null;

        var scoresResponse = await _osuApiV2.Beatmaps.GetUserBeatmapScores(beatmapId.Value,
            userResponse.UserExtend!.Id.Value,
            new GetUserBeatmapScoresQueryParameters { Ruleset = playmode.Value.ToRuleset() });

        if (scoresResponse?.Scores?.Length == 0)
        {
            areScoresFromOtherRuleset = true;
            scoresResponse = await _osuApiV2.Beatmaps.GetUserBeatmapScores(beatmapId.Value,
                userResponse.UserExtend!.Id.Value,
                new GetUserBeatmapScoresQueryParameters()); // Ruleset defaults to beatmaps ruleset
        }

        if (scoresResponse is null)
        {
            await waitMessage.EditAsync(Context.BotClient, language.error_noRecords);
            return;
        }

        if (areScoresFromOtherRuleset && scoresResponse.Scores!.Length != 0)
            beatmapPlaymode =
                (Playmode)(await _osuApiV2.Beatmaps.GetBeatmap(beatmapId.Value))!.BeatmapExtended!.ModeInt!.Value;

        var scores = scoresResponse.Scores!.GroupBy(s => string.Join("", s.Mods!.Select(m => m.Acronym)))
            .Select(m => m.MaxBy(s => s.Pp)!).OrderByDescending(m => m.Pp).ToArray();

        if (scores.Length == 0)
        {
            await waitMessage.EditAsync(Context.BotClient, language.error_noRecords);
            return;
        }

        var beatmap = (await _osuApiV2.Beatmaps.GetBeatmap(scores.First().BeatmapId!.Value))!.BeatmapExtended!;
        if (beatmapset is null) beatmapset = await _osuApiV2.Beatmapsets.GetBeatmapset(beatmap.BeatmapsetId.Value);
        chatInDatabase!.LastBeatmapId = beatmap.Id;

        var textToSend =
            $"{UserHelper.GetUserProfileUrlWrappedInUsernameString(userResponse.UserExtend!.Id.Value, $"<b>{osuUsernameForScore}</b>")}\n\n";
        Playmode currentPlaymode = beatmapPlaymode ?? playmode.Value;
        for (var i = 0; i <= scores.Length - 1; i++)
        {
            var score = scores[i];
            
            textToSend += language.command_score.Fill([
                $"{ScoreHelper.GetScoreRankEmoji(score.Rank)}{ScoreHelper.ParseScoreRank(score.Rank!)}",
                $"{beatmap.Url}",
                $"{beatmapset.Title.EncodeHtml()}",
                $"{beatmap.Version.EncodeHtml()}",
                $"{beatmap.Status}",
                $"{ScoreHelper.GetScoreStatisticsText(score.Statistics!, currentPlaymode)}",
                $"{score.Statistics!.Miss}",
                $"{ScoreHelper.GetFormattedNumConsideringNull(score.Accuracy * 100, round: false)}",
                $"{ScoreHelper.GetModsText(score.Mods!)}",
                $"{score.MaxCombo}",
                $"{beatmap.MaxCombo}",
                $"{ScoreHelper.GetFormattedNumConsideringNull(score.Pp)}",
                $"({score.EndedAt!.Value:dd.MM.yyyy HH:mm}) {ScoreHelper.GetScoreUrlWrappedInString(score.Id!.Value, "link")}"
            ]);
        }

        await waitMessage.EditAsync(Context.BotClient, textToSend, splitValue: "\n\n");
    }
}