using System.Runtime.ConstrainedExecution;

namespace SosuBot.Localization.Languages;

public sealed class Deutsch : ILocalization
{
    public string settings => "Einstellungen:";
    public string settings_language_changedSuccessfully => "Die Sprache wurde auf Deutsch umgestellt.";
    public string settings_language_ru => "Русский";
    public string settings_language_en => "English";
    public string settings_language_de => "Deutsch";

    public string command_start =>
        $"Ein Hilfsbot für osu!-Spieler\n" +
        $"/help - vollständige Befehlsliste anzeigen.\n\n" +
        $"Nutze /botlang, um die Bot-Sprache zu ändern.\n\n" +
        $"Wenn du Bugs oder Feature-Ideen hast, kontaktiere den Entwickler: @Shoukkoo";

    public string command_lang => "Wähle die Bot-Sprache:";

    public string command_help =>
        $"<blockquote expandable>Befehle:\n" +
        $"<b>Wichtig! Wenn dein Nickname Leerzeichen enthält, ersetze sie durch \"_\". Beispiel: \"Blue Archive\" -> \"Blue_Archive\"</b>\n\n" +
        $"/botlang - Sprache im Bot ändern.\n" +
        $"/set [nickname] - Nickname im Bot setzen/ändern.\n" +
        $"/mode [gamemode] - Standard-Spielmodus ändern.\n" +
        $"/user [nickname] - Kurzinfos zu einem Spieler per Username.\n" +
        $"/userid [user_id] - Kurzinfos zu einem Spieler per User-ID.\n" +
        $"/last [nickname] [count] - letzte Plays.\n" +
        $"/lastpassed [nickname] [count] - /last nur mit bestandenen Scores.\n" +
        $"/score [beatmap_link] - deine Records auf dieser Map.\n" +
        $"/userbest [nickname] [gamemode] - beste Plays des Spielers.\n" +
        $"/compare [nickname1] [nickname2] [gamemode] - Spieler vergleichen.\n" +
        $"/chatstats [gamemode] - Top 10 Spieler im Chat.\n" +
        $"/exclude [nickname] - Spieler aus Chat Top 10 ausschließen.\n" +
        $"/include [nickname] - Spieler wieder in Chat Top 10 aufnehmen.\n" +
        $"/chatstats [gamemode] - Top 10 Spieler im Chat.\n" +
        $"/ranking [RU/UZ/country_code] - Top 20 Spieler eines Landes (oder global).\n" +
        $"/daily_stats - exklusiv für Usbekistan: tägliche Score-Statistik.\n" +
        $"/track [users1-3] - Benachrichtigung über neue Top50-Scores dieser Spieler.\n" +
        $"/render - Replay-Rendering.\n" +
        $"/settings - Renderer-Einstellungen.\n" +
        $"/setskin - deinen Skin an den Bot senden.\n" +
        $"/info - letzte Infos zu deinem osu!-Profil vom Bot.\n" +
        $"/calc [x100] [x50] [xMiss] [mods] - Rechner für die PP der ausgewählten Std Map mit diesen Werten.\n" +
        $"/calcmania [x300] [x200] [x100] [x50] [xMiss] [mods] - Rechner für die PP der ausgewählten Mania Map mit diesen Werten.\n" +
        $"\n" +
        $"Wenn du einen Beatmap-Link sendest, schickt der Bot kurze Map-Infos.\n" +
        $"Um das zu verhindern, hänge ein Minus ans Linkende ('-').\n" +
        $"\n" +
        $"Fragen und Vorschläge: @Shoukkoo</blockquote>";

    public string command_last => Shared.CommandLast + " abgeschlossen";

    public string command_set =>
        "Du bist jetzt <b>{}</b>, {}pp💪\n" +
        "Dein Spielmodus: <b>{}</b>🎮\n" +
        "\n" +
        "Zum Ändern deines Standardmodus nutze:\n" +
        "<b>/mode</b> osu/taiko/mania/catch";

    public string command_setMode => "Dein Standardmodus: <b>{}</b>🎮";

    public string command_score => Shared.CommandScore;

    public string command_user => Shared.CommandUser;

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

    public string command_userbest => Shared.CommandUserBest;

    public string command_chatstats_title => "Top-10 osu-Spieler (<b>{}</b>) in dieser Gruppe:\n\n";
    public string command_chatstats_row => "<b>{}. {}</b>: <i>{}pp💪</i>\n";
    public string command_chatstats_end => "\nNutze <b>/user</b>, um dein <b>pp</b> in dieser Liste zu aktualisieren.";
    public string command_excluded => "<b>{}</b> wurde aus /chatstats ausgeschlossen";
    public string command_included => "<b>{}</b> erscheint wieder in /chatstats";

    public string send_mapInfo => Shared.MapInfo;

    public string send_dailyStatistic =>
        "<b>🇺🇿 Bericht seit {}:</b>\n\n" +
        "<b>Aktive Spieler:</b> {}\n" +
        "<b>Bestandene Scores:</b> {}\n" +
        "<b>Einzigartig gespielte Maps:</b> {}\n\n" +
        "<b>💅 Top-5 Farmer:</b>\n" +
        "{}\n" +
        "<b>🔥 Top-5 Aktivste Spieler:</b>\n" +
        "{}\n" +
        "<b>🎯 Top-5 Gespielte Maps:</b>\n" +
        "{}";
    public string daily_stats_count_scores => "Scores";
    public string daily_stats_max_pp => "max.";
    public string daily_stats_tashkent_time => "(Taschkent-Zeit)";

    public string waiting => "Bitte kurz warten...";

    public string error_baseMessage => "Ups... etwas ist schiefgelaufen.";
    public string error_userNotSetHimself => "Wer bist du? Nutze\n/set nickname";
    public string error_hintReplaceSpaces => "<b>Hinweis: </b>Enthält ein Nickname Leerzeichen, ersetze sie durch '_'. (Blue Archive => Blue_Archive)";
    public string error_nameIsEmpty => "Dieser Befehl kann nicht ohne Parameter verwendet werden.\nNutze <b>/set</b> dein_nickname";
    public string error_modeIsEmpty => "Dieser Befehl kann nicht ohne Parameter verwendet werden.\nNutze <b>/mode</b> osu/mania/taiko/ctb";
    public string error_modeIncorrect => "Ungültiger Spielmodus.\nVerfügbare Modi: <b>osu/mania/taiko/ctb</b>";
    public string error_userNotFound => $"{error_baseMessage}\nBenutzer nicht gefunden";
    public string error_specificUserNotFound => $"{error_baseMessage}\n" + "Benutzer {} nicht gefunden";
    public string error_userNotFoundInBotsDatabase => $"{error_baseMessage}\nBenutzer nicht in der Bot-Datenbank gefunden";
    public string error_noRecords => $"{error_baseMessage}\nKeine Records gefunden";
    public string error_noRankings => $"{error_baseMessage}\nKeine Rangliste für diese Anfrage gefunden";
    public string error_argsLength => $"{error_baseMessage}\nUngültige Anzahl von Argumenten";
    public string error_noPreviousScores => $"{error_baseMessage}\nDieser Benutzer hat in den letzten 24 Stunden keine Plays in {{}}";
    public string error_noBestScores => $"{error_baseMessage}\nDieser Benutzer hat noch keine Best-Scores";
    public string error_excludeListAlreadyContainsThisId => "Dieser Benutzer ist bereits aus /chatstats ausgeschlossen";
    public string error_userWasNotExcluded => "Dieser Benutzer war nicht aus /chatstats ausgeschlossen";
    public string error_beatmapNotFound => "Beatmap nicht gefunden";
    public string error_dontUseTelegramUsername => "Du musst den Benutzernamen des osu! Spielers eingeben.";

    public string common_rateLimitSlowDown => "Etwas langsamer bitte!";
    public string common_back => "Zurück";

    public string callback_songPreviewNotFound => "Song-Vorschau wurde nicht gefunden";
    public string callback_songPreviewRequestedBy => "Angefordert von: {}";
    public string callback_renderRequestNotFound => "Render-Anfrage wurde in der Datenbank nicht gefunden";
    public string callback_rendererDownloadingReplay => "Renderer lädt das Replay herunter...";
    public string callback_rendererDownloadingBeatmap => "Renderer lädt das Beatmap herunter...";
    public string callback_rendererInitializing => "Initialisierung. Warte auf freien Renderer...";
    public string callback_renderFinishedPercent => "Render ist zu {} abgeschlossen";
    public string callback_rendererUploadingVideo => "Renderer lädt Video hoch...";

    public string command_dailyStats_usage => "/daily_stats osu/catch/taiko/mania";
    public string command_ranking_title => "Top-Spieler in {}:\n\n";

    public string render_settings_title => "Render-Einstellungen";
    public string render_settings_generalVolume => "Gesamtlautstärke";
    public string render_settings_musicVolume => "Musiklautstärke";
    public string render_settings_effectsVolume => "Effektlautstärke";
    public string render_settings_backgroundDim => "Hintergrundabdunklung";
    public string render_settings_pickSkin => "Skin auswählen";
    public string render_settings_pickCustomSkin => "Eigenen Skin auswählen";
    public string render_settings_useSetSkin => "Nutze /setskin";
    public string render_settings_serverOfflineUseSetSkin => "Bot-Server ist offline. Nutze /setskin für eigenen Skin";
    public string render_settings_privateOnly => "Nutze diesen Befehl nur im privaten Chat mit dem Bot.";
    public string render_settings_useScrollSpeed => "Gib dem Bot /scroll deine_Zahl ein. Zum Beispiel /scroll 25.0";
    public string render_settings_useCursorChange => "Gib dem Bot /cursor deine_Zahl ein. Zum Beispiel /cursor 1.3";
    public string render_settings_invalidScrollSpeed => "Das scheint nicht die richtige Bildlaufgeschwindigkeit zu sein. Bereich: 1.0 bis 40.0";
    public string render_settings_invalidCursorSize => "Das scheint nicht die richtige Größe des Cursors zu sein. Bereich: 0.1 bis 2.0";
    public string render_settings_scrollSpeedUpdated => "Deine Scroll-Geschwindigkeit wurde auf {} geändert.";
    public string render_settings_cursorSizeUpdated => "Deine Cursor-Größe wurde auf {} geändert.";

    public string render_skin_replyToOskFile => "Dieser Befehl muss zusammen mit einer Skin-Datei oder als Reaktion darauf verwendet werden";
    public string render_skin_maxSize => "Skin-Größe muss unter 150 MB liegen";
    public string render_skin_uploadError => "Skin-Upload fehlgeschlagen. Der Skin ist möglicherweise zu groß.\nBitte melde den Fehler dem Bot-Ersteller.";
    public string render_skin_uploadSuccess => "Skin erfolgreich hochgeladen und als Standard gesetzt.";

    public string track_usage => "/track [user1..3]\n/track rm";
    public string track_cleared => "Tracking-Liste wurde geleert.";
    public string track_maxPlayersPerGroup => "Maximal {} Spieler pro Gruppe";
    public string track_nowTrackingPlayers => "Diese Gruppe verfolgt jetzt neue Top-Scores (aus Top50) für:\n{}";

    public string update_rateLimit => "Dieser Befehl kann maximal 5-mal pro 24h verwendet werden. Bitte warte kurz.";
    public string update_onlyInfoAllowed => "Nur /info ist erlaubt";
    public string update_info_header => "Letzte Infos zu <b>{}</b>";
    public string update_info_lastUpdate => "Letzte Aktualisierung dieser Statistik: {} {}";
    public string update_info_trackingSince => "- <b>Der Bot</b> verfolgt deine Scores seit <b><u>{}</u></b> {}";
    public string update_info_trackedScoresSince => "- <b>Seitdem</b> kennt der Bot <b><u>{}</u></b> deiner Scores (alle Mods)";
    public string update_info_lastScoreAt => "- <a href=\"{}\"><b>Dein letzter Score</b></a> wurde am <b><u>{}</u></b> {} gesetzt";
    public string update_info_scoresThisMonth => "- <b>Diesen Monat</b> hast du <b><u>{}</u></b> Scores gesetzt";
    public string update_info_newTopPlaysThisMonth => "- <b>Diesen Monat</b> hast du <b><u>{}</u></b> neue Top-Plays gesetzt (<i>{}</i>)";
    public string update_info_detailedStats => "- <b>Detaillierte Statistik:</b> <a href=\"{}\">Link</a>";
    public string update_info_lastTopScoresTitle => "<b>{} deiner neuesten Top-Scores:</b>";
    public string update_info_lastTopScoresEntry => "<b>#{}</b> - {} - {} {}";

    public string calc_onlySupportsModeMaps => "Dieser Befehl unterstützt nur {} Maps";
    public string calc_tooManyObjects => "Beatmap hat zu viele Objekte.";
    public string calc_invalidScoreStats => "Ungültige Score-Statistiken";
    public string calc_std_usage => "/calc x100 x50 xMiss [mods]\nDie ersten drei Parameter müssen Zahlen sein. Mods (HDDT) sind optional";
    public string calc_mania_usage => "/calc x300 x200 x100 x50 xMiss [mods]\nDie ersten Parameter müssen Zahlen sein. Mods (HDDT) sind optional";

    public string last_usage => "/last nickname count\n/last Shoukko 5";
    public string last_unknownModsNoPp => "Der Score enthält dem Bot unbekannte Mods; pp-Berechnung ist nicht verfügbar.";
    public string last_tooManyObjectsLimitedInfo => "Die Beatmap hat zu viele Objekte; verfügbare Infos sind eingeschränkt.";
    public string last_humanizerCulture => "de-DE";

    public string group_onlyForGroups => "Nur für Gruppen.";
    public string group_onlyForAdmins => "Nur für Admins.";

    public string beatmapLeaderboard_adminOnly => "Zugriff verweigert.";
    public string beatmapLeaderboard_lastBeatmapNotFound => "Der Bot konnte die letzte Beatmap in diesem Chat nicht finden";
    public string beatmapLeaderboard_failedBeatmapInfo => "Beatmap-Informationen konnten nicht geladen werden.";
    public string beatmapLeaderboard_progress => "{} Spieler im Chat gefunden...\nPrüfe die Scores jedes Spielers auf der Map.\n\nDas dauert ca. {}s...";
    public string beatmapLeaderboard_noScoresFromChat => "Keine Scores auf dieser Map von Spielern aus diesem Chat.";

    public string admin_accessDenied => "Zugriff verweigert.";
    public string admin_unknownCommand => "Unbekannter Befehl";
    public string admin_countFormat => "{}: {}";
    public string admin_chatsSummary => "chats: {}/{}";

    public string score_noLeaderboardNoOnlineScores => "Wenn die Map kein Leaderboard hat, hat niemand Online-Scores darauf.";
    public string score_replayAvailable => "Um diesen Score zu rendern, sende bitte /render als Antwort auf diese Nachricht.";
    
    public string replayRender_rateLimit => "Langsamer! Maximal 10 Anfragen pro Stunde sind erlaubt.";
    public string replayRender_serverDown => "Sieht aus, als wäre der Server gerade offline. Versuch es später erneut.";
    public string replayRender_noRenderers => "Derzeit keine freien Renderer verfügbar. Bitte später erneut versuchen.";
    public string replayRender_scoreNotFound => "<a href=\"{}\">Score</a> wurde nicht gefunden";
    public string replayRender_scoreHasNoReplay => "<a href=\"{}\">Score</a> hat kein Replay";
    public string replayRender_usage => "Nutze diesen Befehl auf eine Replay-Datei oder einen Score mit Replay.\nOder gib nach dem Befehl einen Score-Link an.";
    public string replayRender_skinNotFound => "Dein gewählter Skin wurde auf dem Server vermutlich nicht gefunden. Wähle einen anderen.";
    public string replayRender_beatmapNotFound => "Die Beatmap aus diesem Replay ist weder auf den Spiegelseiten noch auf der osu! Website zu finden. Vielleicht handelt es sich um eine lokale Karte?";
    public string replayRender_statusButton => "Status";
    public string replayRender_cancelButton => "Abbrechen";
    public string replayRender_settingsAd => "Benutze /settings, um deinen Renderer einzustellen.";
    public string replayRender_usingExperimentalRenderer => "Da es sich nicht um osu!std handelt, wird fürs Rendern ein experimenteller Renderer verwendet.";
    public string replayRender_onlineQueueSearching => "Aktuell online Renderer: {}\n\nWarteschlange: {}\nSuche freien Renderer...\n\n" + replayRender_settingsAd;
    public string replayRender_noRenderersLeft => "Gerade sind keine freien Renderer mehr da, versuch es später :(";
    public string replayRender_onlineQueueSearchingAgain => "Aktuell online Renderer: {}\n\nWarteschlange: {}\nSuche freien Renderer...\n\n" + replayRender_settingsAd;
    public string replayRender_onlineRendererInProcess => "Aktuell online Renderer: {}\n\n<b>Renderer:</b> {}\n<b>GPU</b>: {}\nRendering läuft...\n\n" + replayRender_settingsAd;
    public string replayRender_onlineSearchingNewRenderer => "Aktuell online Renderer: {}\n\nSuche neuen Renderer...\n\n" + replayRender_settingsAd;
    public string replayRender_timeout => "Timeout. Rendering wurde nicht in {} Sekunden abgeschlossen. Bitte erneut versuchen.";
    public string replayRender_onlyOsuStd => "Rendering ist nur für osu!std verfügbar";
    public string replayRender_errorWithReason => "Render fehlgeschlagen.\n{}";
    public string replayRender_finishedWithLink => "Render abgeschlossen.\n<a href=\"{}\">Videolink</a>";
    public string replayRender_cancelled => "Render abgebrochen.";
    public string replayRender_beatmapLengthTooLong => "Die Karte in diesem Replay dauert zu lange. Probiere eine Karte aus, die weniger als 30 Minuten dauert. Für den experimentellen Renderer ist die Obergrenze auf 20 Minuten festgelegt.";
    
    public string text_songPreviewButton => "Song preview";
    public string text_tooManyObjectsNoPp => "Beatmap hat zu viele Objekte; pp-Berechnung wird übersprungen.";
    public string text_beatmapLinkSkipLog => "Beatmap-Link endet auf '-', pp-Berechnung wird übersprungen. Link: {Link}";

    public string render_menu_generalVolume => "Gesamtlautstärke";
    public string render_menu_music => "Musik";
    public string render_menu_effects => "Effekte";
    public string render_menu_background => "Hintergrund";
    public string render_menu_skipIntro => "Intro überspringen";
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
    public string render_menu_resetSettings => "Einstellungen zurücksetzen";
    public string render_menu_scrollSpeed => "Scroll Speed";
    public string render_menu_scrollDirection => "Scroll Direction";
    public string render_menu_cursorSize => "Cursor Size";
    public string render_cancel_success => "Der Render-Vorgang wurde erfolgreich abgebrochen";
}
