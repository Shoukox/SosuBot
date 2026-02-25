namespace SosuBot.Localization.Languages;

public sealed class English : ILocalization
{
    public string settings => "Settings:";
    public string settings_language_changedSuccessfully => "Language has been changed to English.";
    public string settings_language_ru => "Русский";
    public string settings_language_en => "English";
    public string settings_language_de => "Deutsch";

    public string command_start =>
        $"A helper bot for osu! players\n" +
        $"/help - get the full list of commands.\n" +
        $"Use /lang to change bot language.\n\n" +
        $"If you find bugs or have feature suggestions, contact the creator: @Shoukkoo";

    public string command_lang => "Choose bot language:";

    public string command_help =>
        $"<blockquote expandable>Commands:\n" +
        $"<b>Important! If your nickname contains spaces, replace them with \"_\". Example: \"Blue Archive\" -> \"Blue_Archive\"</b>\n\n" +
        $"/set [nickname] - add/change your nickname in the bot.\n" +
        $"/mode [gamemode] - change your default game mode.\n" +
        $"/user [nickname] - short info about a player by username.\n" +
        $"/userid [user_id] - short info about a player by user id.\n" +
        $"/last [nickname] [count] - latest plays.\n" +
        $"/lastpassed [nickname] [count] - /last for passed scores only.\n" +
        $"/score [beatmap_link] - your records on this map.\n" +
        $"/userbest [nickname] [gamemode] - player's best plays.\n" +
        $"/compare [nickname1] [nickname2] [gamemode] - compare players.\n" +
        $"/chatstats [gamemode] - top 10 players in this chat.\n" +
        $"/exclude [nickname] - exclude a user from chat top 10.\n" +
        $"/include [nickname] - include a user back into chat top 10.\n" +
        $"/chatstats [gamemode] - top 10 players in this chat.\n" +
        $"/ranking [RU/UZ/country_code] - top 20 players for a country (or global).\n" +
        $"/daily_stats - Uzbekistan exclusive: daily stats for all scores from all players in the country.\n" +
        $"/track [users1-3] - bot notifies you about new top50 scores of these players.\n" +
        $"/render - replay rendering.\n" +
        $"/settings - replay renderer settings.\n" +
        $"/setskin - send your skin to the bot.\n" +
        $"/info - latest info about your osu profile from the bot.\n" +
        $"\n" +
        $"If you send a beatmap link, the bot sends short map information.\n" +
        $"To prevent that, add a minus at the end of the link ('-').\n" +
        $"\n" +
        $"Questions and suggestions: @Shoukkoo</blockquote>";

    public string command_last =>
        "{GlobalRank}🎵{}<b>({})</b> <a href=\"https://osu.ppy.sh/beatmaps/{}\">{} [{}]</a> <b>({}; {}⭐️)</b>\n" +
        "{}/{}❌ - <b><i>{}</i></b>%🎯{OptionalNewLine}\n" +
        "<b>➕{}</b> <i><b>{}x/{}x</b></i> <b><u>{}pp💪</u></b>\n" +
        "(<b><u>{}</u></b>) {link}\n" +
        "{} | {}% passed";

    public string command_set =>
        "Now you are <b>{}</b>, {}pp💪\n" +
        "Your game mode: <b>{}</b>🎮\n" +
        "\n" +
        "To change your default mode use:\n" +
        "<b>/mode</b> osu/taiko/mania/catch";

    public string command_setMode => "Your default mode: <b>{}</b>🎮";

    public string command_score =>
        "🎵<b>({})</b> <a href=\"{}\">{} [{}]</a> <b>({})</b>\n" +
        "{}/{}❌ - <b><i>{}</i></b>%🎯\n" +
        "<b>➕{}</b> <i><b>{}x/{}x</b></i> <b><u>{}pp💪</u></b>\n" +
        "{}\n\n";

    public string command_user =>
        "<b>{}</b>\n" +
        "<i>{}</i>\n\n" +
        "🌐<b>rank</b>: <i>#{} (#{} {})</i>\n" +
        "💪<b>pp</b>: <i>{} {}</i>\n" +
        "🎯<b>accuracy</b>: <i>{}</i>\n" +
        "🔢<b>playcount</b>: <i>{}</i>\n" +
        "⏱️<b>playtime</b>: <i>{}h</i>\n" +
        "📍<b>registered</b>: <i>{}</i>\n" +
        "🏆<b>achievements</b>: <i>{}/{}</i>\n\n" +
        "<i>{}</i> <b>SSH</b>⚪️ - <i>{}</i> <b>SH</b>⚪️\n" +
        "<i>{}</i> <b>SS</b>🟡 - <i>{}</i> <b>S</b>🟡 - <i>{}</i> <b>A</b>🟢";

    public string command_compare =>
        "<pre>" +
        "{}\n\n" +
        "🐺{}  🐺{}\n" +
        "🌐{}   🌐{}\n" +
        "🌐{}   🌐{}\n" +
        "💪{}  💪{}\n" +
        "🎯{}  🎯{}\n" +
        "⏱️{}  ⏱️{}\n" +
        "</pre>";

    public string command_userbest =>
        "{}. 🎵(<b>{}</b>) <a href=\"http://osu.ppy.sh/b/{}\">{} [{}]</a> (<b>{}</b>)\n" +
        "{}/{}❌ - <b><i>{}</i></b>%🎯\n" +
        "<b>➕{}</b> <i><b>{}x</b>{}</i> <b><u>{}pp💪</u></b>\n\n";

    public string command_chatstats_title => "Top-10 osu players (<b>{}</b>) in this group:\n\n";
    public string command_chatstats_row => "<b>{}. {}</b>: <i>{}pp💪</i>\n";
    public string command_chatstats_end => "\nUse <b>/user</b> to update your <b>pp</b> in this list.";
    public string command_excluded => "<b>{}</b> has been excluded from /chatstats";
    public string command_included => "<b>{}</b> will appear in /chatstats again";

    public string send_mapInfo =>
        "<b>{}</b>\n" +
        "<b>[{}]</b> - {}⭐️ - {} - {} - <b>{}</b> - <a href=\"https://osu.ppy.sh/beatmaps/{}\">link</a>\n" +
        "<b>CS</b>: {} | <b>AR</b>: {} | <b>HP</b>: {} | <b>BPM</b>: {}\n\n" +
        "<b>+{} ({}⭐️) pp calculation:</b>\n" +
        "<code>" +
        "acc      | classic\n" +
        "---------+---------\n" +
        "{}" +
        "{}" +
        "{}" +
        "</code>\n\n" +
        "<code>" +
        "acc      | lazer\n" +
        "---------+---------\n" +
        "{}" +
        "{}" +
        "{}" +
        "</code>\n";

    public string send_dailyStatistic =>
        "<b>🇺🇿 Report since {}:</b>\n\n" +
        "<b>Active players:</b> {}\n" +
        "<b>Passed scores:</b> {}\n" +
        "<b>Unique played maps:</b> {}\n\n" +
        "<b>💅 Top-5 farmers:</b>\n" +
        "{}\n" +
        "<b>🔥 Top-5 active players:</b>\n" +
        "{}\n" +
        "<b>🎯 Top-5 played maps:</b>\n" +
        "{}";
    public string daily_stats_count_scores => "scores";
    public string daily_stats_max_pp => "max.";
    public string daily_stats_tashkent_time => "(Tashkent time)";

    public string waiting => "Please wait a bit...";

    public string error_baseMessage => "Oops... something went wrong.";
    public string error_userNotSetHimself => "Who are you? Use\n/set nickname";

    public string error_hintReplaceSpaces =>
        "<b>Hint: </b>if a nickname contains spaces, replace them with '_'. (Blue Archive => Blue_Archive)";

    public string error_nameIsEmpty =>
        "This command cannot be used without parameters.\nType <b>/set</b> your_nickname";

    public string error_modeIsEmpty =>
        "This command cannot be used without parameters.\nType <b>/mode</b> osu/mania/taiko/ctb";

    public string error_modeIncorrect => "Invalid game mode.\nAvailable modes: <b>osu/mania/taiko/ctb</b>";
    public string error_userNotFound => $"{error_baseMessage}\nUser not found";
    public string error_specificUserNotFound => $"{error_baseMessage}\n" + "User {} not found";
    public string error_userNotFoundInBotsDatabase => $"{error_baseMessage}\nUser not found in bot database";
    public string error_noRecords => $"{error_baseMessage}\nRecords not found";
    public string error_noRankings => $"{error_baseMessage}\nRanking table for this query was not found";
    public string error_argsLength => $"{error_baseMessage}\nInvalid number of arguments";
    public string error_noPreviousScores => $"{error_baseMessage}\nThis user has no plays in the last 24 hours in {{}}";
    public string error_noBestScores => $"{error_baseMessage}\nThis user has no best scores yet";
    public string error_excludeListAlreadyContainsThisId => "This user is already excluded from /chatstats";
    public string error_userWasNotExcluded => "This user was not excluded from /chatstats";
    public string error_beatmapNotFound => "Beatmap not found";

    public string common_rateLimitSlowDown => "Slow down a bit!";
    public string common_back => "Back";

    public string callback_songPreviewNotFound => "Song preview was not found";
    public string callback_songPreviewRequestedBy => "Requested by: {}";
    public string callback_renderRequestNotFound => "Render request was not found in the database";
    public string callback_rendererUploadingReplay => "Renderer is uploading replay...";
    public string callback_rendererUploadingBeatmap => "Renderer is uploading beatmap...";
    public string callback_rendererInitializing => "Initializing. Waiting for a free renderer...";
    public string callback_renderFinishedPercent => "Render is {} complete";
    public string callback_rendererUploadingVideo => "Renderer is uploading video...";

    public string command_dailyStats_usage => "/daily_stats osu/catch/taiko/mania";
    public string command_ranking_title => "Top players in {}:\n\n";

    public string render_settings_title => "Render settings";
    public string render_settings_generalVolume => "General volume";
    public string render_settings_musicVolume => "Music volume";
    public string render_settings_effectsVolume => "Effects volume";
    public string render_settings_backgroundDim => "Background dim";
    public string render_settings_pickSkin => "Choose skin";
    public string render_settings_pickCustomSkin => "Choose custom skin";
    public string render_settings_useSetSkin => "Use /setskin";
    public string render_settings_serverOfflineUseSetSkin => "Bot server is offline. Use /setskin for custom skin";
    public string render_settings_privateOnly => "Use this command only in a private chat with the bot.";

    public string render_skin_replyToOskFile => "Use this command as a reply to a .osk skin file";
    public string render_skin_maxSize => "Skin size must be below 150 MB";
    public string render_skin_uploadError => "Skin upload failed. The skin may be too large.\nPlease report this issue to the bot creator.";
    public string render_skin_uploadSuccess => "Skin uploaded successfully and set as default.";

    public string track_usage => "/track [user1..3]\n/track rm";
    public string track_cleared => "Tracking list has been cleared.";
    public string track_maxPlayersPerGroup => "Maximum {} players per group";
    public string track_nowTrackingPlayers => "This group now tracks new top scores (from top50) for:\n{}";

    public string update_rateLimit => "This command can be used at most 5 times per 24 hours. Please wait a bit.";
    public string update_onlyInfoAllowed => "Only /info is allowed";

    public string calc_onlySupportsModeMaps => "This command supports only {} maps";
    public string calc_tooManyObjects => "Beatmap has too many objects.";
    public string calc_invalidScoreStats => "Invalid score statistics";
    public string calc_std_usage => "/calc x100 x50 xMiss [mods]\nFirst three parameters must be numbers. Mods (HDDT) are optional";
    public string calc_mania_usage => "/calc x300 x200 x100 x50 xMiss [mods]\nFirst parameters must be numbers. Mods (HDDT) are optional";

    public string last_usage => "/last nickname count\n/last Shoukko 5";
    public string last_unknownModsNoPp => "Score contains mods unknown to the bot; pp calculation is unavailable.";
    public string last_tooManyObjectsLimitedInfo => "Beatmap has too many objects; available info will be limited.";
    public string last_humanizerCulture => "en-US";

    public string group_onlyForGroups => "Only for groups.";
    public string group_onlyForAdmins => "Only for admins.";

    public string beatmapLeaderboard_adminOnly => "Access denied.";
    public string beatmapLeaderboard_lastBeatmapNotFound => "Bot could not find the last beatmap in this chat";
    public string beatmapLeaderboard_failedBeatmapInfo => "Failed to get beatmap information.";
    public string beatmapLeaderboard_progress => "Found {} players in the chat...\nChecking each player's scores on the map.\n\nThis will take around {}s...";
    public string beatmapLeaderboard_noScoresFromChat => "No scores on this map from players in this chat.";

    public string admin_accessDenied => "Access denied.";
    public string admin_unknownCommand => "Unknown command";
    public string admin_countFormat => "{}: {}";
    public string admin_chatsSummary => "chats: {}/{}";

    public string score_noLeaderboardNoOnlineScores => "If the map has no leaderboard, nobody has online scores on it.";

    public string replayRender_rateLimit => "Slow down! Maximum 10 requests per hour are allowed.";
    public string replayRender_serverDown => "Looks like the server is currently down. Try again later.";
    public string replayRender_noRenderers => "No available renderers right now. Please try again later.";
    public string replayRender_scoreNotFound => "<a href=\"{}\">Score</a> was not found";
    public string replayRender_scoreHasNoReplay => "<a href=\"{}\">Score</a> has no replay";
    public string replayRender_usage => "Use this command on a replay file or a score with replay.\nOr provide a score link after the command.";
    public string replayRender_skinNotFound => "Your selected skin was likely not found on the server. Choose another one.";
    public string replayRender_statusButton => "Status";
    public string replayRender_onlineQueueSearching => "Current online renderers: {}\n\nQueue: {}\nLooking for a free renderer...";
    public string replayRender_noRenderersLeft => "No free renderers left right now, try again later :(";
    public string replayRender_onlineQueueSearchingAgain => "Current online renderers: {}\n\nQueue: {}\nLooking for a free renderer...";
    public string replayRender_onlineRendererInProcess => "Current online renderers: {}\n\n<b>Renderer:</b> {}\n<b>GPU</b>: {}\nRendering in progress...";
    public string replayRender_onlineSearchingNewRenderer => "Current online renderers: {}\n\nLooking for a new renderer...";
    public string replayRender_timeout => "Timeout. Rendering was not completed in {} seconds. Please retry.";
    public string replayRender_onlyOsuStd => "Rendering is available only for osu!std";
    public string replayRender_errorWithReason => "Render failed.\n{}";
    public string replayRender_finishedWithLink => "Render completed.\n<a href=\"{}\">Video link</a>";

    public string text_songPreviewButton => "Song preview";
    public string text_tooManyObjectsNoPp => "Beatmap has too many objects; pp calculation is skipped.";
    public string text_beatmapLinkSkipLog => "Beatmap link ends with '-', skipping pp calculation. Link: {Link}";

    public string render_menu_generalVolume => "General volume";
    public string render_menu_music => "Music";
    public string render_menu_effects => "Effects";
    public string render_menu_background => "Background";
    public string render_menu_skin => "Skin";
    public string render_menu_urBar => "UR Bar";
    public string render_menu_aimErrorCircle => "Aim Error Circle";
    public string render_menu_motionBlur => "Motion Blur";
    public string render_menu_hpBar => "HP Bar";
    public string render_menu_showPp => "Show PP";
    public string render_menu_hitCounter => "Hit Counter";
    public string render_menu_ignoreFails => "Ignore Fails";
    public string render_menu_video => "Video";
    public string render_menu_storyboard => "Storyboard";
    public string render_menu_mods => "Mods";
    public string render_menu_keys => "Keys";
    public string render_menu_combo => "Combo";
    public string render_menu_leaderboard => "Leaderboard";
    public string render_menu_strainGraph => "Strain Graph";
    public string render_menu_useExperimentalRenderer => "Use Experimental Renderer";
    public string render_menu_resetSettings => "Reset settings";
}
