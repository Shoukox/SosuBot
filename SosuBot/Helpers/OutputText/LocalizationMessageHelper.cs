using Humanizer;
using OsuApi.BanchoV2.Users.Models;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.Localization;
using System.Globalization;

namespace SosuBot.Helpers.OutputText;

public static class LocalizationMessageHelper
{
    public static string CallbackSongPreviewRequestedBy(ILocalization language, string requestedBy)
        => language.callback_songPreviewRequestedBy.Fill([requestedBy]);

    public static string CallbackRenderFinishedPercent(ILocalization language, string percent)
        => language.callback_renderFinishedPercent.Fill([percent]);

    public static string AdminCountFormat(ILocalization language, string entity, string count)
        => language.admin_countFormat.Fill([entity, count]);

    public static string CommandRankingTitle(ILocalization language, string flagEmoji)
        => language.command_ranking_title.Fill([flagEmoji]);

    public static string AdminChatsSummary(ILocalization language, string chats, string total)
        => language.admin_chatsSummary.Fill([chats, total]);

    public static string CalcOnlySupportsModeMaps(ILocalization language, string gameMode)
        => language.calc_onlySupportsModeMaps.Fill([gameMode]);

    public static string BeatmapLeaderboardProgress(ILocalization language, string playersCount, string seconds)
        => language.beatmapLeaderboard_progress.Fill([playersCount, seconds]);

    public static string ChatstatsTitle(ILocalization language, string gameMode)
        => language.command_chatstats_title.Fill([gameMode]);

    public static string ChatstatsRow(ILocalization language, string index, string username, string pp)
        => language.command_chatstats_row.Fill([index, username, pp]);

    public static string ChatstatsExcluded(ILocalization language, string username)
        => language.command_excluded.Fill([username]);

    public static string ChatstatsIncluded(ILocalization language, string username)
        => language.command_included.Fill([username]);

    public static string ErrorSpecificUserNotFound(ILocalization language, string username)
        => language.error_specificUserNotFound.Fill([username]);

    public static string CommandCompare(ILocalization language, params string[] values)
        => language.command_compare.Fill(values);

    public static string ErrorNoPreviousScores(ILocalization language, string gameMode)
        => language.error_noPreviousScores.Fill([gameMode]);

    public static string CommandLast(ILocalization language, params string[] values)
        => language.command_last.Fill(values);

    public static string CommandSetMode(ILocalization language, string gameMode)
        => language.command_setMode.Fill([gameMode]);

    public static string CommandScore(ILocalization language, params string[] values)
        => language.command_score.Fill(values);

    public static string CommandSet(ILocalization language, params string[] values)
        => language.command_set.Fill(values);

    public static string CommandUserBest(ILocalization language, params string[] values)
        => language.command_userbest.Fill(values);

    public static string ReplayScoreNotFound(ILocalization language, string scoreLink)
        => language.replayRender_scoreNotFound.Fill([scoreLink]);

    public static string ReplayScoreHasNoReplay(ILocalization language, string scoreLink)
        => language.replayRender_scoreHasNoReplay.Fill([scoreLink]);

    public static string ReplayOnlineQueueSearching(ILocalization language, string onlineRenderersCount, string queue)
        => language.replayRender_onlineQueueSearching.Fill([onlineRenderersCount, queue]);

    public static string ReplayOnlineQueueSearchingAgain(ILocalization language, string onlineRenderersCount, string queue)
        => language.replayRender_onlineQueueSearchingAgain.Fill([onlineRenderersCount, queue]);

    public static string ReplayRendererInProcess(ILocalization language, string onlineRenderersCount, string rendererName, string gpu)
        => language.replayRender_onlineRendererInProcess.Fill([onlineRenderersCount, rendererName, gpu]);

    public static string ReplaySearchingNewRenderer(ILocalization language, string onlineRenderersCount)
        => language.replayRender_onlineSearchingNewRenderer.Fill([onlineRenderersCount]);

    public static string ReplayTimeout(ILocalization language, string timeoutSeconds)
        => language.replayRender_timeout.Fill([timeoutSeconds]);

    public static string ReplayErrorWithReason(ILocalization language, string reason)
        => language.replayRender_errorWithReason.Fill([reason]);

    public static string ReplayFinishedWithLink(ILocalization language, string watchUrl)
        => language.replayRender_finishedWithLink.Fill([watchUrl]);

    public static string TrackMaxPlayersPerGroup(ILocalization language, string maxArgsCount)
        => language.track_maxPlayersPerGroup.Fill([maxArgsCount]);

    public static string TrackNowTrackingPlayers(ILocalization language, string nicknames)
        => language.track_nowTrackingPlayers.Fill([nicknames]);

    public static string SendMapInfo(ILocalization language, params string[] values)
        => language.send_mapInfo.Fill(values);

    public static string LastScoreEndedAgo(ILocalization language, DateTime endedAtUtc)
    {
        var culture = CultureInfo.GetCultureInfoByIetfLanguageTag(language.last_humanizerCulture);
        return endedAtUtc.Humanize(dateToCompareAgainst: DateTime.UtcNow, culture: culture);
    }

    public static string UserProfileText(
        ILocalization language,
        ScoreHelper scoreHelper,
        UserExtend user,
        Playmode playmode,
        double? currentPp,
        string ppDifferenceText,
        string achievementsTotalText)
    {
        DateTime.TryParse(user.JoinDate?.Value, out var registerDateTime);
        int achievementsCount = user.UserAchievements?.Length ?? 0;

        return language.command_user.Fill([
            $"{playmode.ToGamemode()}",
            $"{UserHelper.GetUserProfileUrlWrappedInUsernameString(user.Id!.Value, user.Username!)}",
            $"{UserHelper.GetUserRankText(user.Statistics!.GlobalRank)}",
            $"{UserHelper.GetUserRankText(user.Statistics.CountryRank)}",
            $"{UserHelper.CountryCodeToFlag(user.CountryCode ?? "nn")}",
            $"{scoreHelper.GetFormattedNumConsideringNull(currentPp)}",
            $"{ppDifferenceText}",
            $"{user.Statistics.HitAccuracy:N2}%",
            $"{user.Statistics.PlayCount:N0}",
            $"{user.Statistics.PlayTime / 3600}",
            $"{registerDateTime:dd.MM.yyyy HH:mm:ss}",
            $"{achievementsCount}",
            achievementsTotalText,
            $"{user.Statistics.GradeCounts!.SSH}",
            $"{user.Statistics.GradeCounts!.SH}",
            $"{user.Statistics.GradeCounts!.SS}",
            $"{user.Statistics.GradeCounts!.S}",
            $"{user.Statistics.GradeCounts!.A}"
        ]);
    }
}
