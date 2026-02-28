namespace SosuBot.Localization.Languages;

public sealed class Russian : ILocalization
{
    public string settings => "Настройки:";
    public string settings_language_changedSuccessfully => "Язык успешно изменен на русский.";
    public string settings_language_ru => "Русский";
    public string settings_language_en => "English";
    public string settings_language_de => "Deutsch";

    public string command_start =>
        $"Бот-помощник для игроков osu!\n" +
        $"/help - для получения списка всех комманд.\n\n" +
        $"Для смены языка бота используй /botlang\n\n" +
        $"Если вы найдете какие-либо проблемы или предложения по расширению функционала бота, пишите моему создателю - @Shoukkoo";

    public string command_lang => "Выбери язык бота:";

    public string command_help =>
        $"<blockquote expandable>Команды:\n" +
        $"<b>Важно! Если в вашем нике есть пробелы, заменяйте их на \"_\". Например, \"Blue Archive\" -> \"Blue_Archive\"</b>\n\n" +
        $"/botlang - изменить язык в боте.\n" +
        $"/set [nickname] - добавить\\изменить свой ник в боте.\n" +
        $"/mode [gamemode] - изменить игровой режим по умолчанию.\n" +
        $"/user [nickname] - краткая информация об игроке с данным юзернеймом.\n" +
        $"/userid [user_id] - краткая информация об игроке с данным user id.\n" +
        $"/last [nickname] [count] - последние сыгранные игры.\n" +
        $"/lastpassed [nickname] [count] - /last только для пасснутых скоров.\n" +
        $"/score [beatmap_link] - ваши рекорды на этой карте.\n" +
        $"/userbest [nickname] [gamemode] - лучшие игры игрока.\n" +
        $"/compare [nickname1] [nickname2] [gamemode] - сравнить игроков.\n" +
        $"/chatstats [gamemode] - топ10 игроков в чате.\n" +
        $"/exclude [nickname] - исключить юзера из топ10 игроков в чате.\n" +
        $"/include [nickname] - вернуть юзера в топ10 игроков в чате.\n" +
        $"/chatstats [gamemode] - топ10 игроков в чате.\n" +
        $"/ranking [RU/UZ/country_code] - топ20 игроков для данной страны (либо глобально).\n" +
        $"/daily_stats - эксклюзивно для Узбекистана. Ежедневная статистика по всем скорам от всех игроков в стране.\n" +
        $"/track [users1-3] - бот будет оповещать вас о новых скорах в топ50 данных игроков.\n" +
        $"/render - рендер реплеев.\n" +
        $"/settings - настройки рендера реплеев.\n" +
        $"/setskin - отправить в бота свой скин.\n" +
        $"/info - последняя инфа про ваш осу профиль от бота.\n" +
        $"\n" +
        $"Если отправить ссылку карты, бот отправит краткую информацию о ней\n" +
        $"Чтобы избежать отправки краткой информации, добавьте в конец ссылки минус ('-')\n" +
        $"\n" +
        $"По вопросам и предложениям писать @Shoukkoo</blockquote>";

    public string command_last =>
        "{GlobalRank}🎵{}<b>({})</b> <a href=\"https://osu.ppy.sh/beatmaps/{}\">{} [{}]</a> <b>({}; {}⭐️)</b>\n" +
        "{}/{}❌ - <b><i>{}</i></b>%🎯{OptionalNewLine}\n" +
        "<b>➕{}</b> <i><b>{}x/{}x</b></i> <b><u>{}pp💪</u></b>\n" +
        "(<b><u>{}</u></b>) {link}\n" +
        "{} | {}% пройдено";

    public string command_set =>
        "Теперь ты <b>{}</b>, {}pp💪\n" +
        "Твой игровой режим: <b>{}</b>🎮\n" +
        "\n" +
        "Для смены режима по умолчанию используй:\n" +
        "<b>/mode</b> osu/taiko/mania/catch";

    public string command_setMode => "Твой режим игры по умолчанию: <b>{}</b>🎮";

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
        "🌐{}   🌐{}\n" + //rank
        "🌐{}   🌐{}\n" +
        "💪{}  💪{}\n" +
        "🎯{}  🎯{}\n" +
        "⏱️{}  ⏱️{}\n" +
        "</pre>";

    public string command_userbest =>
        "{}. 🎵(<b>{}</b>) <a href=\"http://osu.ppy.sh/b/{}\">{} [{}]</a> (<b>{}</b>)\n" +
        "{}/{}❌ - <b><i>{}</i></b>%🎯\n" +
        "<b>➕{}</b> <i><b>{}x</b>{}</i> <b><u>{}pp💪</u></b>\n\n";

    public string command_chatstats_title => "Топ-10 осеров (<b>{}</b>) в группе:\n\n";
    public string command_chatstats_row => "<b>{}. {}</b>: <i>{}pp💪</i>\n";
    public string command_chatstats_end => "\nИспользуйте <b>/user</b>, чтобы обновить ваш <b>pp</b> в данном списке.";
    public string command_excluded => "<b>{}</b> был успешно исключен из /chatstats";
    public string command_included => "<b>{}</b> снова будет появляться в /chatstats";

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
        "<b>🇺🇿 Отчёт с момента {}:</b>\n\n" +
        "<b>Активных игроков:</b> {}\n" +
        "<b>Пасснутых скоров:</b> {}\n" +
        "<b>Сыгранных уникальных карт:</b> {}\n\n" +
        "<b>💅 Топ-5 фармеров:</b>\n" +
        "{}\n" +
        "<b>🔥 Топ-5 активных игроков:</b>\n" +
        "{}\n" +
        "<b>🎯 Топ-5 сыгранных карт:</b>\n" +
        "{}";
    public string daily_stats_count_scores => "скоров";
    public string daily_stats_max_pp => "макс.";
    public string daily_stats_tashkent_time => "(по тшк.)";

    public string waiting => "Подожди немного...";

    public string error_baseMessage => "Произошел троллинг...";
    public string error_userNotSetHimself => "Ты кто? Юзай\n/set nickname";

    public string error_hintReplaceSpaces =>
        "<b>Подсказка: </b>если в нике есть пробелы, заменяйте их на '_'. (Blue Archive => Blue_Archive)";

    public string error_nameIsEmpty =>
        "Эта команда не может быть использована без параметров.\nВведи <b>/set</b> твой_никнейм";

    public string error_modeIsEmpty =>
        "Эта команда не может быть использована без параметров.\nВведи <b>/mode</b> osu/mania/taiko/ctb";

    public string error_modeIncorrect => "Неверный режим игры.\nДоступные режимы игры: <b>osu/mania/taiko/ctb</b>";
    public string error_userNotFound => $"{error_baseMessage}\nИгрок не найден";
    public string error_specificUserNotFound => $"{error_baseMessage}\n" + "Игрок {} не найден";
    public string error_userNotFoundInBotsDatabase => $"{error_baseMessage}\nИгрок не найден в базе данных бота";
    public string error_noRecords => $"{error_baseMessage}\nРекорды не найдены";
    public string error_noRankings => $"{error_baseMessage}\nТаблица рейтинга по данному запросу не найдена";
    public string error_argsLength => $"{error_baseMessage}\nНеверное количество аргументов";
    public string error_noPreviousScores => $"{error_baseMessage}\nЭтот пользователь не играл последние 24 часа в {{}}";
    public string error_noBestScores => $"{error_baseMessage}\nУ этого пользователя еще нету лучших рекордов";
    public string error_excludeListAlreadyContainsThisId => "Этот пользователь уже был исключен из /chatstats";
    public string error_userWasNotExcluded => "Этот пользователь и так не был исключен из /chatstats";
    public string error_beatmapNotFound => "Карта не найдена";

    public string common_rateLimitSlowDown => "Давай не так быстро!";
    public string common_back => "Назад";

    public string callback_songPreviewNotFound => "Превью песни не найдено";
    public string callback_songPreviewRequestedBy => "Запрос от: {}";
    public string callback_renderRequestNotFound => "Запрос на рендер не найден в базе данных";
    public string callback_rendererUploadingReplay => "Рендерер загружает реплей...";
    public string callback_rendererUploadingBeatmap => "Рендерер загружает карту...";
    public string callback_rendererInitializing => "Инициализация. Ждем свободного рендерера...";
    public string callback_renderFinishedPercent => "Рендер завершен на {}";
    public string callback_rendererUploadingVideo => "Рендерер загружает видео...";

    public string command_dailyStats_usage => "/daily_stats osu/catch/taiko/mania";
    public string command_ranking_title => "Топ игроков в {}:\n\n";

    public string render_settings_title => "Настройки рендера";
    public string render_settings_generalVolume => "Общая громкость";
    public string render_settings_musicVolume => "Громкость музыки";
    public string render_settings_effectsVolume => "Громкость эффектов";
    public string render_settings_backgroundDim => "Затемнение экрана";
    public string render_settings_pickSkin => "Выбрать скин";
    public string render_settings_pickCustomSkin => "Выбрать свой скин";
    public string render_settings_useSetSkin => "Используйте /setskin";
    public string render_settings_serverOfflineUseSetSkin => "Сервер бота оффлайн. Для кастомного скина используй /setskin";
    public string render_settings_privateOnly => "Только в личке с ботом.";

    public string render_skin_replyToOskFile => "Эту команду нужно использовать ответом на файл скина";
    public string render_skin_maxSize => "Скины не больше 150мб!";
    public string render_skin_uploadError => "Ошибка загрузки скина. Возможно, скин весит слишком много.\nПожалуйста, сообщи создателю об ошибке.";
    public string render_skin_uploadSuccess => "Скин успешно загружен и будет использован по умолчанию!";

    public string track_usage => "/track [user1..3]\n/track rm";
    public string track_cleared => "Лист был очищен.";
    public string track_maxPlayersPerGroup => "Допустимо макс. {} игрока на группу";
    public string track_nowTrackingPlayers => "Теперь в этой группе отслеживаются новые топ скоры (из топ50) следующих игроков:\n{}";

    public string update_rateLimit => "Эту команду можно применять макс. 5 раз за 24 часа. Подожди немного.";
    public string update_onlyInfoAllowed => "Разрешено только /info";

    public string calc_onlySupportsModeMaps => "Эта команда поддерживает только {} карты";
    public string calc_tooManyObjects => "В карте слишком много объектов.";
    public string calc_invalidScoreStats => "Некорректная статистика скора";
    public string calc_std_usage => "/calc x100 x50 xMiss [mods]\nПервые три параметра - цифры. Моды (HDDT) - опциональны";
    public string calc_mania_usage => "/calc x300 x200 x100 x50 xMiss [mods]\nПервые параметры - цифры. Моды (HDDT) - опциональны";

    public string last_usage => "/last nickname count\n/last Shoukko 5";
    public string last_unknownModsNoPp => "В скоре присутствуют неизвестные боту моды, расчет пп невозможен.";
    public string last_tooManyObjectsLimitedInfo => "В карте слишком много объектов, доступная информация будет ограничена.";
    public string last_humanizerCulture => "ru-RU";

    public string group_onlyForGroups => "Только для групп.";
    public string group_onlyForAdmins => "Только для админов.";

    public string beatmapLeaderboard_adminOnly => "Пшол вон!";
    public string beatmapLeaderboard_lastBeatmapNotFound => "Бот не смог найти последнюю карту в чате";
    public string beatmapLeaderboard_failedBeatmapInfo => "Не удалось получить информацию о карте.";
    public string beatmapLeaderboard_progress => "Найдено {} игроков в чате...\nПроверяем скоры каждого на карте.\n\nЭто займет примерно {}сек...";
    public string beatmapLeaderboard_noScoresFromChat => "На этой карте нет скоров от игроков из этого чата.";

    public string admin_accessDenied => "Пшол вон!";
    public string admin_unknownCommand => "Неизвестная команда";
    public string admin_countFormat => "{}: {}";
    public string admin_chatsSummary => "chats: {}/{}";

    public string score_noLeaderboardNoOnlineScores => "Если на карте нет лидерборда, то онлайн скоров на ней нет ни у кого.";

    public string replayRender_rateLimit => "Давай не так быстро! Разрешено максимум 10 запросов за 1 час.";
    public string replayRender_serverDown => "Кажется, сервер сейчас не запущен. Попробуй в другой раз";
    public string replayRender_noRenderers => "Нету ни одного доступного рендерера для рендера реплеев. Попробуй в другой раз";
    public string replayRender_scoreNotFound => "<a href=\"{}\">Скор</a> не найден";
    public string replayRender_scoreHasNoReplay => "<a href=\"{}\">Скор</a> не имеет реплея";
    public string replayRender_usage => "Используй эту команду на реплей файл или на скор с реплеем.\nЛибо укажи ссылку на скор после команды.";
    public string replayRender_skinNotFound => "Вероятно, твой выбранный скин не был найден на сервере - выбери другой.";
    public string replayRender_statusButton => "Статус";
    public string replayRender_onlineQueueSearching => "Текущее количество онлайн рендереров: {}\n\nОчередь: {}\nИщем свободный рендерер...";
    public string replayRender_noRenderersLeft => "Сейчас свободных рендереров не осталось, попробуй позже :(";
    public string replayRender_onlineQueueSearchingAgain => "Текущее количество онлайн рендереров: {}\n\nОчередь: {}\nИщем свободный рендерер...";
    public string replayRender_onlineRendererInProcess => "Текущее количество онлайн рендереров: {}\n\n<b>Рендерер:</b> {}\n<b>Видеокарта</b>: {}\nРендер в процессе...";
    public string replayRender_onlineSearchingNewRenderer => "Текущее количество онлайн рендереров: {}\n\nИщем нового рендерера...";
    public string replayRender_timeout => "Таймаут. Рендеринг не был завершен за {} секунд, повторите попытку.";
    public string replayRender_onlyOsuStd => "Рендер доступен только для osu!std";
    public string replayRender_errorWithReason => "Ошибка рендера.\n{}";
    public string replayRender_finishedWithLink => "Рендер завершен.\n<a href=\"{}\">Ссылка на видео</a>";

    public string text_songPreviewButton => "Song preview";
    public string text_tooManyObjectsNoPp => "В карте слишком много объектов, пп расчет не будет проведен.";
    public string text_beatmapLinkSkipLog => "Beatmap link ends with '-', skipping pp calculation. Link: {Link}";

    public string render_menu_generalVolume => "Общая громкость";
    public string render_menu_music => "Музыка";
    public string render_menu_effects => "Эффекты";
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
    public string render_menu_resetSettings => "Сбросить настройки";
}