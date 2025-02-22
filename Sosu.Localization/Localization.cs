﻿
namespace Sosu.Localization
{
    public class Localization
    {
        public class Methods
        {
            public static ILocalization GetLang(string lang)
            {
                ILocalization language = new Russian();
                switch (lang)
                {
                    case "ru":
                        language = new Russian();
                        break;
                    case "en":
                        language = new Russian();
                        break;
                }
                return language;
            }
            public static string ReplaceEmpty(string text, IEnumerable<string> replace)
            {
                foreach (var item in replace)
                {
                    int ind = text.IndexOf("{}");
                    text = text.Remove(ind, 2).Insert(ind, item);
                }
                return text;
            }
        }
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
                $"Комманды:\n" +
                $"<b>Важно! Если в вашем нике есть пробелы, заменяйте их на \"_\". Например, \"Blue Archive\" -> \"Blue_Archive\"</b>\n\n" +
                $"/set [nickname] - добавить\\изменить свой ник в боте.\n" +
                $"/user [nickname] - краткая информация об игроке.\n" +
                $"/last [nickname] [count] - последние сыгранные игры.\n" +
                $"/lss [nickname] [count] - последние сыгранные игры. (нестандартная альтернатива)\n" +
                $"/score [beatmap_link] - ваши рекорды на этой карте.\n" +
                $"/userbest [nickname] [mode] - лучшие игры игрока.\n" +
                $"/compare [nickname1] [nickname2] [mode] - сравнить игроков. [mode]: 0-std, 1-taiko, 2-catch, 3-mania\n" +
                $"/chat_stats - топ10 игроков в чате.\n" +
                $"/settings - изменить язык бота\n\n" +
                $"[mode]: 0-std, 1-taiko, 2-catch, 3-mania\n" +
                $"Если отправить ссылку карты, бот отправит краткую информацию о ней\n\n" +
                $"По вопросам и предложениям писать создателю @Shoukkoo";
            public string command_last =>
                "{}. <b>({})</b> <a href=\"https://osu.ppy.sh/beatmaps/{}\">{} [{}]</a> <b>({})</b>\n" +
                    "{} / {} / {} / {}❌ - <b><i>{}</i></b>%\n" +
                    "<b>{}</b> <i>{}/{}</i> <b><u>~{}pp</u></b> (<b><u>~{}pp</u></b> if FC)\n({}) {}% пройдено\n\n";

            public string command_set => "Теперь ты <b>{}</b>";
            public string command_score =>
                "<b>({})</b> <a href=\"https://osu.ppy.sh/beatmaps/{}\">{} [{}]</a> <b>({})</b>\n" +
                    "{} / {} / {} / {}❌ - <b><i>{}</i></b>%\n" +
                    "<b>{}</b> <i>{}/{}</i> <b><u>{}pp</u></b>\n({})\n\n";
            public string command_user =>
                   "<b>{}</b>\n" +
                "<a href=\"{}\"><i>{}</i></a>\n\n" +
                "<b>rank</b>: <i>#{} (#{} {})</i>\n" +
                "<b>pp</b>: <i>{} {}</i>\n" +
                "<b>accuracy</b>: <i>{}%</i>\n" +
                "<b>plays</b>: <i>{}</i>\n" +
                "<b>playtime</b>: <i>{}h</i>\n\n" +
                "<i>{}</i> <b>SSH</b> - <i>{}</i> <b>SH</b>\n" +
                "<i>{}</i> <b>SS</b> - <i>{}</i> <b>S</b> - <i>{}</i> <b>A</b>";
            public string command_compare =>
                  "<pre>" +
                "{}\n\n" +
                "{}  {}\n" +
                "{}  #{}\n" +
                "{}  #{}\n" +
                "{}  {}pp\n" +
                "{}  {}\n" +
                "{}  {}h\n" +
                "</pre>";
            public string command_userbest =>
                 "{}. (<b>{}</b>) <a href=\"http://osu.ppy.sh/b/{}\">{} [{}]</a> <b>{}</b>\n" +
                    "{} / {} / {} / {}❌ - <b><i>{}</i></b>%\n" +
                    "<b>{}</b> <i>{}/{}</i> <b><u>{}pp</u></b>\n\n";
            public string command_chatstats_title => "Топ-10 осеров в группе:\n\n";
            public string command_chatstats_row => "<b>{}. {}</b>: <i>{}pp</i>\n";
            public string command_chatstats_end => "\nИспользуйте <b>/user</b>, чтобы обновить ваш <b>pp</b> в данном списке.";
            public string command_delete_user_chatstats => "Успешно удалены: <b>{}</b>";
            public string send_mapInfo =>
                     "[{}] - {}* - {} - {} - <b>{}</b>\n" +
                     "<b>CS</b>: {} | <b>AR</b>: {} | <b>OD</b>: {} | <b>HP</b>: {} | <b>BPM</b>: {}\n" +
                     "100% - {}pp | 98% - {}pp | 96% - {}pp\n<b>{}</b>";
            public string waiting => "Подожди немного...";

            public string error_baseMessage => "Произошел троллинг...";
            public string error_noUser => "Ты кто? Юзай\n/set nickname";
            public string error_nameIsEmpty => "Эта команда не может быть использована без параметров.\nВведи /set твой_никнейм";
            public string error_userNotFound => $"{error_baseMessage}\nИгрок не найден";
            public string error_noRecords => $"{error_baseMessage}\nРекорды не найдены";
            public string error_argsLength => $"{error_baseMessage}\nНеверное количество аргументов";
            public string error_noPreviousScores => $"{error_baseMessage}\nПрошлых рекордов не существует";
        }
    }
}
