using System;
using System.Collections.Generic;
using System.Text;

namespace SosuBot.Localization.Languages
{
    public static class Shared
    {
        public static string CommandLast =
            "{GlobalRank}🎵{}<b>({})</b> <a href=\"https://osu.ppy.sh/beatmaps/{}\">{} [{}]</a> <b>({}; {}⭐️)</b>\n" +
            "{}/{}❌ - <b><i>{}</i></b>%🎯{OptionalNewLine}\n" +
            "<b>➕{}</b> <i><b>{}x/{}x</b></i> <b><u>{}pp💪</u></b>\n" +
            "(<b><u>{}</u></b>) {link}\n" +
            "{} | {}%";

        public static string CommandScore =
            "🎵<b>({})</b> <a href=\"{}\">{} [{}]</a> <b>({})</b>\n" +
            "{}/{}❌ - <b><i>{}</i></b>%🎯\n" +
            "<b>➕{}</b> <i><b>{}x/{}x</b></i> <b><u>{}pp💪</u></b>\n" +
            "{}\n\n";

        public static string CommandUser =
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

        public static string CommandUserBest =
            "{}. 🎵(<b>{}</b>) <a href=\"http://osu.ppy.sh/b/{}\">{} [{}]</a> (<b>{}</b>)\n" +
            "{}/{}❌ - <b><i>{}</i></b>%🎯\n" +
            "<b>➕{}</b> <i><b>{}x</b>{}</i> <b><u>{}pp💪</u></b>\n\n";

        public static string MapInfo =
            "<b>{}</b>\n" +
            "<b>+{} [{}]</b> - {}⭐️ - {} - {} - <b>{}</b> - <a href=\"https://osu.ppy.sh/beatmaps/{}\">link</a>\n" +
            "<b>CS</b>: {} | <b>AR</b>: {} | <b>HP</b>: {} | <b>BPM</b>: {}\n\n" +
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
    }
}
