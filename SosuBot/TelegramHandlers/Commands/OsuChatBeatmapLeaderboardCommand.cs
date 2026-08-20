using Microsoft.Extensions.DependencyInjection;
using OsuApi.BanchoV2;
using OsuApi.BanchoV2.Clients.Beatmaps.HttpIO;
using OsuApi.BanchoV2.Models;
using OsuApi.BanchoV2.Users.Models;
using SosuBot.Database;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.Helpers;
using SosuBot.Localization;
using SosuBot.Services.Synchronization;
using SosuBot.TelegramHandlers.Abstract;
using Telegram.Bot.Types;

namespace SosuBot.TelegramHandlers.Commands;

public sealed class OsuChatBeatmapLeaderboardCommand : CommandBase<Message>
{
    public static readonly string[] Commands = ["/beatmap_leaderboard", "/bl"];
    public static readonly string Description = "таблица лидеров чата на карте";
    private const string BeatmapLeaderboardApiRateLimitKey = "command:beatmap-leaderboard:osu-api";
    private BanchoApiV2 _osuApiV2 = null!;
    private ScoreHelper _scoreHelper = null!;
    private CachingHelper _cachingHelper = null!;
    private RateLimiterFactory _rateLimiterFactory = null!;
    private TokenBucketRateLimiter _beatmapLeaderboardApiRateLimiter = null!;
    private BotContext _database = null!;

    public override async Task BeforeExecuteAsync()
    {
        await base.BeforeExecuteAsync();
        _osuApiV2 = Context.ServiceProvider.GetRequiredService<BanchoApiV2>();
        _scoreHelper = Context.ServiceProvider.GetRequiredService<ScoreHelper>();
        _cachingHelper = Context.ServiceProvider.GetRequiredService<CachingHelper>();
        _rateLimiterFactory = Context.ServiceProvider.GetRequiredService<RateLimiterFactory>();
        _beatmapLeaderboardApiRateLimiter =
            _rateLimiterFactory.Get(RateLimiterFactory.RateLimitPolicy.BeatmapLeaderboard);
        _database = Context.ServiceProvider.GetRequiredService<BotContext>();
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

        // OsuUser? osuUserInDatabase = await _database.OsuUsers.FindAsync(Context.Update.From!.Id);
        // if (osuUserInDatabase is null || !osuUserInDatabase.IsAdmin)
        // {
        //     await Context.Update.ReplyAsync(Context.BotClient, language.beatmapLeaderboard_adminOnly);
        //     return;
        // }
        TelegramChat? chatInDatabase = await _database.TelegramChats.FindAsync(Context.Update.Chat.Id);
        Message waitMessage = await Context.Update.ReplyAsync(Context.BotClient, language.waiting);

        int? beatmapId = chatInDatabase!.LastBeatmapId;
        BeatmapsetExtended? beatmapset = null;
        if (Context.Update.ReplyToMessage != null)
        {
            // bl with reply
            var link = OsuHelper.ParseOsuBeatmapLink(Context.Update.ReplyToMessage?.GetAllLinks(), out var beatmapsetId, out beatmapId);
            if (link is not null)
            {
                if (beatmapId is null && beatmapsetId is not null)
                {
                    await WaitForBeatmapLeaderboardApiRequestAsync();
                    beatmapset = await _cachingHelper.GetOrCacheBeatmapset(beatmapsetId.Value, _osuApiV2);
                    beatmapId = beatmapset!.Beatmaps![0].Id;
                }
            }
        }

        if (beatmapId == null)
        {
            await waitMessage.EditAsync(Context.BotClient, language.error_baseMessage + "\n" + language.beatmapLeaderboard_lastBeatmapNotFound);
            return;
        }

        await WaitForBeatmapLeaderboardApiRequestAsync();
        BeatmapExtended? beatmap = await _cachingHelper.GetOrCacheBeatmap(beatmapId!.Value, _osuApiV2);
        if (beatmap == null)
        {
            await waitMessage.EditAsync(Context.BotClient, language.beatmapLeaderboard_failedBeatmapInfo);
            return;
        }

        if (beatmapset is null && beatmap.BeatmapsetId is { } beatmapsetIdForTitle)
        {
            await WaitForBeatmapLeaderboardApiRequestAsync();
            beatmapset = await _cachingHelper.GetOrCacheBeatmapset(beatmapsetIdForTitle, _osuApiV2);
        }

        var foundChatMembers = new List<OsuUser>();
        chatInDatabase!.ExcludeFromChatstats = chatInDatabase.ExcludeFromChatstats ?? new List<long>();
        foreach (var memberId in chatInDatabase.ChatMembers!)
        {
            OsuUser? foundMember = await _database.OsuUsers.FindAsync(memberId);
            if (foundMember != null && !chatInDatabase.ExcludeFromChatstats.Contains(foundMember.OsuUserId))
                foundChatMembers.Add(foundMember);
        }
        foundChatMembers = foundChatMembers.DistinctBy(m => m.OsuUserId).ToList();

        int estimatedSeconds = foundChatMembers.Count + (beatmapset is null ? 0 : 1);
        await waitMessage.EditAsync(Context.BotClient, LocalizationMessageHelper.BeatmapLeaderboardProgress(language,
            $"{foundChatMembers.Count}",
            $"{estimatedSeconds:N0}"
        ));

        Playmode playmode = (Playmode)(beatmap.ModeInt ?? 0);
        string ruleset = playmode.ToRuleset();
        List<OsuApi.BanchoV2.Models.Score> foundScores = new();
        foreach (OsuUser osuUser in foundChatMembers)
        {
            await WaitForBeatmapLeaderboardApiRequestAsync();
            GetUserBeatmapScoreResponse? scores = await _osuApiV2.Beatmaps.GetUserBeatmapScore(beatmapId.Value, osuUser.OsuUserId, new() { Mode = ruleset });
            if (scores?.BeatmapUserScore?.Score is { } score)
            {
                foundScores.Add(score);
            }
        }
        foundScores = foundScores.OrderByDescending(s => s.TotalScore).ToList();

        string beatmapTitle = beatmapset?.Title?.EncodeHtml() ?? "—";
        string beatmapDifficulty = beatmap.Version?.EncodeHtml() ?? "—";
        string beatmapStars = _scoreHelper.GetFormattedNumConsideringNull(
            beatmap.DifficultyRating,
            format: "N2",
            round: false);
        string sendMessage = $"<i>({ruleset.ParseRulesetToGamemode()})</i> <b>{beatmapTitle} [{beatmapDifficulty}]</b> — <b>{beatmapStars}⭐️</b>\n\n";

        for (int i = 0; i < foundScores.Count; i++)
        {
            Score score = foundScores[i];
            sendMessage += $"{i + 1}. <b>{score.User?.Username}</b> - <b><i>{_scoreHelper.GetFormattedNumConsideringNull(score.Accuracy * 100, round: false)}</i></b>%🎯 - {score.Statistics!.Miss}❌ - <b><u>{_scoreHelper.GetScoreUrlWrappedInString(score.Id!.Value, $"{score.Pp:N2}pp")}💪</u></b>\n";
        }

        if (foundScores.Count == 0)
        {
            sendMessage += language.beatmapLeaderboard_noScoresFromChat;
        }

        await waitMessage.EditAsync(Context.BotClient, sendMessage);
    }

    private Task WaitForBeatmapLeaderboardApiRequestAsync() =>
        _beatmapLeaderboardApiRateLimiter.WaitAsync(
            BeatmapLeaderboardApiRateLimitKey,
            Context.CancellationToken);
}
