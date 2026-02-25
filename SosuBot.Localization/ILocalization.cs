// ReSharper disable InconsistentNaming

namespace SosuBot.Localization;

public interface ILocalization
{
    public string command_start { get; }
    public string command_help { get; }
    public string command_last { get; }
    public string command_set { get; }
    public string command_setMode { get; }
    public string command_score { get; }
    public string command_user { get; }
    public string command_compare { get; }
    public string command_userbest { get; }
    public string command_chatstats_title { get; }
    public string command_chatstats_row { get; }
    public string command_chatstats_end { get; }
    public string command_excluded { get; }
    public string command_included { get; }
    public string settings { get; }
    public string settings_language_ru { get; }
    public string settings_language_en { get; }
    public string settings_language_changedSuccessfully { get; }
    public string send_mapInfo { get; }
    public string send_dailyStatistic { get; }
    public string waiting { get; }

    public string error_baseMessage { get; }
    public string error_userNotSetHimself { get; }
    public string error_hintReplaceSpaces { get; }
    public string error_nameIsEmpty { get; }
    public string error_modeIsEmpty { get; }
    public string error_modeIncorrect { get; }
    public string error_userNotFound { get; }
    public string error_specificUserNotFound { get; }
    public string error_userNotFoundInBotsDatabase { get; }
    public string error_noRecords { get; }
    public string error_noRankings { get; }
    public string error_argsLength { get; }
    public string error_noPreviousScores { get; }
    public string error_noBestScores { get; }
    public string error_excludeListAlreadyContainsThisId { get; }
    public string error_userWasNotExcluded { get; }
    public string error_beatmapNotFound { get; }

    public string common_rateLimitSlowDown { get; }
    public string common_back { get; }

    public string callback_songPreviewNotFound { get; }
    public string callback_songPreviewRequestedBy { get; }
    public string callback_renderRequestNotFound { get; }
    public string callback_rendererUploadingReplay { get; }
    public string callback_rendererUploadingBeatmap { get; }
    public string callback_rendererInitializing { get; }
    public string callback_renderFinishedPercent { get; }
    public string callback_rendererUploadingVideo { get; }

    public string command_dailyStats_usage { get; }
    public string command_ranking_title { get; }

    public string render_settings_title { get; }
    public string render_settings_generalVolume { get; }
    public string render_settings_musicVolume { get; }
    public string render_settings_effectsVolume { get; }
    public string render_settings_backgroundDim { get; }
    public string render_settings_pickSkin { get; }
    public string render_settings_pickCustomSkin { get; }
    public string render_settings_useSetSkin { get; }
    public string render_settings_serverOfflineUseSetSkin { get; }
    public string render_settings_privateOnly { get; }

    public string render_skin_replyToOskFile { get; }
    public string render_skin_maxSize { get; }
    public string render_skin_uploadError { get; }
    public string render_skin_uploadSuccess { get; }

    public string track_usage { get; }
    public string track_cleared { get; }
    public string track_maxPlayersPerGroup { get; }
    public string track_nowTrackingPlayers { get; }

    public string update_rateLimit { get; }
    public string update_onlyInfoAllowed { get; }

    public string calc_onlySupportsModeMaps { get; }
    public string calc_tooManyObjects { get; }
    public string calc_invalidScoreStats { get; }
    public string calc_std_usage { get; }
    public string calc_mania_usage { get; }

    public string last_usage { get; }
    public string last_unknownModsNoPp { get; }
    public string last_tooManyObjectsLimitedInfo { get; }

    public string group_onlyForGroups { get; }
    public string group_onlyForAdmins { get; }

    public string beatmapLeaderboard_adminOnly { get; }
    public string beatmapLeaderboard_lastBeatmapNotFound { get; }
    public string beatmapLeaderboard_failedBeatmapInfo { get; }
    public string beatmapLeaderboard_progress { get; }
    public string beatmapLeaderboard_noScoresFromChat { get; }

    public string admin_accessDenied { get; }
    public string admin_unknownCommand { get; }
    public string admin_countFormat { get; }
    public string admin_chatsSummary { get; }

    public string score_noLeaderboardNoOnlineScores { get; }

    public string replayRender_rateLimit { get; }
    public string replayRender_serverDown { get; }
    public string replayRender_noRenderers { get; }
    public string replayRender_scoreNotFound { get; }
    public string replayRender_scoreHasNoReplay { get; }
    public string replayRender_usage { get; }
    public string replayRender_skinNotFound { get; }
    public string replayRender_statusButton { get; }
    public string replayRender_onlineQueueSearching { get; }
    public string replayRender_noRenderersLeft { get; }
    public string replayRender_onlineQueueSearchingAgain { get; }
    public string replayRender_onlineRendererInProcess { get; }
    public string replayRender_onlineSearchingNewRenderer { get; }
    public string replayRender_timeout { get; }
    public string replayRender_onlyOsuStd { get; }
    public string replayRender_errorWithReason { get; }
    public string replayRender_finishedWithLink { get; }

    public string text_songPreviewButton { get; }
    public string text_tooManyObjectsNoPp { get; }
    public string text_beatmapLinkSkipLog { get; }

    public string render_menu_generalVolume { get; }
    public string render_menu_music { get; }
    public string render_menu_effects { get; }
    public string render_menu_background { get; }
    public string render_menu_skin { get; }
    public string render_menu_urBar { get; }
    public string render_menu_aimErrorCircle { get; }
    public string render_menu_motionBlur { get; }
    public string render_menu_hpBar { get; }
    public string render_menu_showPp { get; }
    public string render_menu_hitCounter { get; }
    public string render_menu_ignoreFails { get; }
    public string render_menu_video { get; }
    public string render_menu_storyboard { get; }
    public string render_menu_mods { get; }
    public string render_menu_keys { get; }
    public string render_menu_combo { get; }
    public string render_menu_leaderboard { get; }
    public string render_menu_strainGraph { get; }
    public string render_menu_useExperimentalRenderer { get; }
    public string render_menu_resetSettings { get; }
}