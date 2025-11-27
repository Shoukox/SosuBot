using Microsoft.Extensions.DependencyInjection;
using OsuApi.V2;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.Helpers.OutputText;
using SosuBot.Helpers.Types;
using SosuBot.Localization;
using SosuBot.Localization.Languages;
using SosuBot.Services.Handlers.Abstract;
using Telegram.Bot.Types;

namespace SosuBot.Services.Handlers.Commands;

public sealed class OsuChatBeatmapLeaderboardCommand : CommandBase<Message>
{
    public static readonly string[] Commands = ["/beatmap_leaderboard", "/bl"];
    private ApiV2 _osuApiV2 = null!;

    private static readonly IEqualityComparer<OsuUser> OsuUserComparer = EqualityComparer<OsuUser>.Create((u1, u2) => u1?.OsuUserId == u2?.OsuUserId, u => u.GetHashCode());

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

        var osuUserInDatabase = await Context.Database.OsuUsers.FindAsync(Context.Update.From!.Id);
        if (osuUserInDatabase is null || !osuUserInDatabase.IsAdmin)
        {
            await Context.Update.ReplyAsync(Context.BotClient, "Пшол вон!");
            return;
        }

        ILocalization language = new Russian();
        var chatInDatabase = await Context.Database.TelegramChats.FindAsync(Context.Update.Chat.Id);
        var waitMessage = await Context.Update.ReplyAsync(Context.BotClient, language.waiting);

        int? beatmapId = chatInDatabase!.LastBeatmapId;
        if (Context.Update.ReplyToMessage != null)
        {
            // bl with reply
            var link = OsuHelper.ParseOsuBeatmapLink(Context.Update.ReplyToMessage?.GetAllLinks(), out var beatmapsetId, out beatmapId);
            if (link is not null)
            {
                if (beatmapId is null && beatmapsetId is not null)
                {
                    var beatmapset = await _osuApiV2.Beatmapsets.GetBeatmapset(beatmapsetId.Value);
                    beatmapId = beatmapset.Beatmaps![0].Id;
                }
            }
        }

        if (beatmapId == null)
        {
            await waitMessage.EditAsync(Context.BotClient, language.error_baseMessage + "\nБот не смог найти последнюю карту в чате");
            return;
        }

        var beatmap = await _osuApiV2.Beatmaps.GetBeatmap(beatmapId.Value);
        if (beatmap == null)
        {
            await waitMessage.EditAsync(Context.BotClient, "Не удалось получить информацию о карте.");
            return;
        }

        var foundChatMembers = new List<OsuUser>();
        chatInDatabase!.ExcludeFromChatstats = chatInDatabase.ExcludeFromChatstats ?? new List<long>();
        foreach (var memberId in chatInDatabase.ChatMembers!)
        {
            var foundMember = await Context.Database.OsuUsers.FindAsync(memberId);
            if (foundMember != null && !chatInDatabase.ExcludeFromChatstats.Contains(foundMember.OsuUserId))
                foundChatMembers.Add(foundMember);
        }
        foundChatMembers = foundChatMembers.Distinct(OsuUserComparer).ToList();

        // Fake delay to avoid hitting rate limits
        await Task.Delay(1000);

        int delayPerUser = 1000; // 1 second per user
        int delay = delayPerUser * foundChatMembers.Count;
        await waitMessage.EditAsync(Context.BotClient, $"Найдено {foundChatMembers.Count} игроков в чате...\nПроверяем скоры каждого на карте.\n\nЭто займет примерно {delay / 1000f:N0}сек...");

        Playmode playmode = (Playmode)(beatmap.BeatmapExtended!.ModeInt ?? 0);
        string ruleset = playmode.ToRuleset();
        string sendMessage = "";
        List<OsuApi.V2.Models.Score> foundScores = new();
        foreach (var osuUser in foundChatMembers)
        {
            var scores = await _osuApiV2.Beatmaps.GetUserBeatmapScore(beatmapId.Value, osuUser.OsuUserId, new() { Mode = ruleset });
            if (scores?.BeatmapUserScore?.Score is { } score)
            {
                foundScores.Add(score);
            }

            await Task.Delay(delayPerUser); // Delay between each user to avoid rate limits
        }
        foundScores = foundScores.OrderByDescending(s => s.TotalScore).ToList();

        for (int i = 0; i < foundScores.Count; i++)
        {
            var score = foundScores[i];
            sendMessage += $"{i + 1}. <b>{score.User?.Username}</b> - <b><i>{ScoreHelper.GetFormattedNumConsideringNull(score.Accuracy * 100, round: false)}</i></b>%🎯 - {score.Statistics!.Miss}❌ - <b><u>{ScoreHelper.GetScoreUrlWrappedInString(score.Id!.Value, $"{score.Pp:N2}pp")}💪</u></b>\n";
        }

        if(foundScores.Count == 0)
        {
            sendMessage = "На этой карте нет скоров от игроков из этого чата.";
        }

        await Task.Delay(delayPerUser);
        await waitMessage.EditAsync(Context.BotClient, sendMessage);
    }
}