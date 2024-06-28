using Sosu.Services;

namespace Sosu
{
    public class Data
    {
        public static void SaveData()
        {
            string path = $"{Directory.GetCurrentDirectory()}\\config.txt";
            using (StreamWriter sw = new StreamWriter(path, false))
            {
                foreach (var item in Variables.osuUsers)
                {
                    sw.Write($"{item.osuName}==={item.telegramId}\n");
                }
            }
        }
        public static void LoadData()
        {
            //var osuusers = variables.db.getdata("select * from osuusers", 3);
            //var osuchats = variables.db.getdata("select * from osuchats", 4);

            //foreach (var item in osuusers)
            //{
            //    Variables.osuUsers.Add(new Sosu.Types.osuUser(telegramId: (long)(item[0]), osuName: (string)item[1] ?? "", pp: (double)item[2]));
            //}
            //foreach (var item in osuchats)
            //{
            //    var members = item[2] is DBNull ? new List<long>() : ((long[])item[2]).ToList();
            //    Variables.chats.Add(new Sosu.Types.Chat(new Telegram.Bot.Types.Chat { Id = (long)(item[1]) }, (int)(item[0]), members = members, (string)(item[3]) ?? "ru"));
            //}
            TextDatabase.LoadData();
            Console.WriteLine($"groups: {Variables.chats.Count}, users: {Variables.osuUsers.Count}");
        }
    }
}
