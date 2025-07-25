namespace SosuBot.Localization.Languages
{
    public struct Russian : ILocalization
    {
        public string settings => "Настройки:";
        public string settings_language_changedSuccessfully => "Язык успешно изменен на русский.";
        public string settings_language_ru => "Русский";
        public string settings_language_en => "English";
        public string settings_language_de => "Deutsch";
        public string command_start =>
            $"Бот-помощник для игрока osu!\n" +
            $"/help - для получения списка всех комманд.\n\n" +
            $"Если вы найдете какие-либо проблемы или предложения по расширению функционала бота, пишите моему создателю - @Shoukkoo";
        public string command_help =>
            $"Бот-помощник для осера\n\n" +
            $"Команды:\n" +
            $"<b>Важно! Если в вашем нике есть пробелы, заменяйте их на \"_\". Например, \"Blue Archive\" -> \"Blue_Archive\"</b>\n\n" +
            $"/set [nickname] - добавить\\изменить свой ник в боте.\n" +
            $"/mode [gamemode] - изменить игровой режим по умолчанию.\n" +
            $"/user [nickname] - краткая информация об игроке.\n" +
            $"/last [nickname] [count] - последние сыгранные игры.\n" +
            $"/score [beatmap_link] - ваши рекорды на этой карте.\n" +
            $"/userbest [nickname] [gamemode] - лучшие игры игрока.\n" +
            $"/compare [nickname1] [nickname2] [gamemode] - сравнить игроков.\n" +
            $"/chatstats [gamemode] - топ10 игроков в чате.\n" +
            $"\n" +
            $"Если отправить ссылку карты, бот отправит краткую информацию о ней\n\n" +
            $"По вопросам и предложениям писать создателю @Shoukkoo";

        public string command_last =>
            "{}<b>({})</b> <a href=\"https://osu.ppy.sh/beatmaps/{}\">{} [{}]</a> <b>({}, {}⭐️)</b>\n" +
                "{} / {}❌ - <b><i>{}</i></b>%\n" +
                "<b>{}</b> <i>{}/{}</i> <b><u>{}pp</u></b> (<b><u>{}pp/{}pp</u></b> FC/SS)\n({}) {}% пройдено\n\n";

        public string command_set =>
            "Теперь ты <b>{}</b>, {}pp\n" +
            "Твой игровой режим: <b>{}</b>\n" +
            "\n" +
            "Для смены режима по умолчанию используй:\n" +
            "<b>/mode</b> osu/taiko/mania/catch";
        public string command_setMode => "Твой режим игры по умолчанию: <b>{}</b>";
        public string command_score =>
                "<b>({})</b> <a href=\"{}\">{} [{}]</a> <b>({})</b>\n" +
                "{} / {}❌ - <b><i>{}</i></b>%\n" +
                "<b>{}</b> <i>{}/{}</i> <b><u>{}pp</u></b>\n({})\n\n";
        public string command_user =>
                "<b>{}</b>\n" +
                "<a href=\"{}\"><i>{}</i></a>\n\n" +
                "<b>rank</b>: <i>#{} (#{} {})</i>\n" +
                "<b>pp</b>: <i>{} {}</i>\n" +
                "<b>accuracy</b>: <i>{}</i>\n" +
                "<b>plays</b>: <i>{}</i>\n" +
                "<b>playtime</b>: <i>{}h</i>\n\n" +
                "<i>{}</i> <b>SSH</b> - <i>{}</i> <b>SH</b>\n" +
                "<i>{}</i> <b>SS</b> - <i>{}</i> <b>S</b> - <i>{}</i> <b>A</b>";
        public string command_compare =>
              "<pre>" +
            "{}\n\n" +
            "{}  {}\n" +
            "{}  {}\n" +
            "{}  {}\n" +
            "{}  {}\n" +
            "{}  {}\n" +
            "{}  {}\n" +
            "</pre>";
        public string command_userbest =>
                "{}. (<b>{}</b>) <a href=\"http://osu.ppy.sh/b/{}\">{} [{}]</a> (<b>{}</b>)\n" +
                "{} / {}❌ - <b><i>{}</i></b>%\n" +
                "<b>{}</b> <i>{}/{}</i> <b><u>{}pp</u></b>\n\n";
        public string command_chatstats_title => "Топ-10 осеров в группе:\n\n";
        public string command_chatstats_row => "<b>{}. {}</b>: <i>{}pp</i>\n";
        public string command_chatstats_end => "\nИспользуйте <b>/user</b>, чтобы обновить ваш <b>pp</b> в данном списке.";
        public string command_excluded => "<b>{}</b> был успешно исключен из /chatstats";
        public string command_included => "<b>{}</b> снова будет появляться в /chatstats";
        public string send_mapInfo =>
            "<b>{}</b>\n" +
            "<b>[{}]</b> - {}⭐️ - {} - {} - <b>{}</b> - <a href=\"https://osu.ppy.sh/beatmaps/{}\">link</a>\n" +
            "<b>CS</b>: {} | <b>AR</b>: {} | <b>HP</b>: {} | <b>BPM</b>: {}\n\n" +
            "<b>+{} ({}⭐️) pp calculation:</b>\n" +
            "<code>" +
            "acc  | classic | lazer\n" +
            "-----+---------+---------\n" +
            "100% | {}pp   | {}pp\n" +
            "99%  | {}pp   | {}pp\n" +
            "98%  | {}pp   | {}pp\n" +
            "</code>\n";

        public string send_dailyStatistic =>
            "<b>🇺🇿 Отчёт с момента {}UTC:</b>\n\n" +
            "<b>Активных игроков:</b> {}\n" +
            "<b>Пасснутых скоров:</b> {}\n" +
            "<b>Сыгранных уникальных карт:</b> {}\n" +
            "<b>Самый большой скор по pp:</b> <b><a href=\"{}\">{}pp</a></b> от <a href=\"{}\">{}</a>\n\n" +
            "<b>🔥 Топ-5 активных игроков:</b>\n" +
            "{}\n" +
            "<b>🎯 Топ-5 сыгранных карт:</b>\n" +
            "{}";
        public string waiting => "Подожди немного...";

        public string error_baseMessage => "Произошел троллинг...";
        public string error_userNotSetHimself => "Ты кто? Юзай\n/set nickname";
        public string error_hintReplaceSpaces => "<b>Подсказка: </b>если в нике есть пробелы, заменяйте их на '_'. (Blue Archive => Blue_Archive)";
        public string error_nameIsEmpty => "Эта команда не может быть использована без параметров.\nВведи <b>/set</b> твой_никнейм";
        public string error_modeIsEmpty => "Эта команда не может быть использована без параметров.\nВведи <b>/mode</b> osu/mania/taiko/ctb";
        public string error_modeIncorrect => "Неверный режим игры.\nДоступные режимы игры: <b>osu/mania/taiko/ctb</b>";
        public string error_userNotFound => $"{error_baseMessage}\nИгрок не найден";
        public string error_specificUserNotFound => $"{error_baseMessage}\n" + "Игрок {} не найден";
        public string error_userNotFoundInBotsDatabase => $"{error_baseMessage}\nИгрок не найден в базе данных бота";
        public string error_noRecords => $"{error_baseMessage}\nРекорды не найдены";
        public string error_argsLength => $"{error_baseMessage}\nНеверное количество аргументов";
        public string error_noPreviousScores => $"{error_baseMessage}\nЭтот пользователь не играл последние 24 часа в {{}}";
        public string error_noBestScores => $"{error_baseMessage}\nУ этого пользователя еще нету лучших рекордов";
        public string error_excludeListAlreadyContainsThisId => $"Этот пользователь уже был исключен из /chatstats";
        public string error_userWasNotExcluded => "Этот пользователь и так не был исключен из /chatstats";
    }
}
