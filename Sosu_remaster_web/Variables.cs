using Sosu.osu.V1;

namespace Sosu
{
    public class Variables
    {
        public static BotConfiguration botConfiguration;
        public static Telegram.Bot.Types.User bot;

        public static osuApi osuApi;
        //public static Database db;
        public static List<long> WHITELIST = new List<long>() { 728384906 };

        public static List<Types.osuUser> osuUsers;
        public static List<Types.Chat> chats;
    }
}
