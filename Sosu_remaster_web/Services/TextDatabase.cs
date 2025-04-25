using Sosu.Types;

namespace Sosu.Services
{
    public class TextDatabase
    {
        public static void SaveData()
        {
            using (StreamWriter sw = new StreamWriter("osuusers.txt"))
            {
                sw.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(Variables.osuUsers));
            }
            using (StreamWriter sw = new StreamWriter("chats.txt"))
            {
                sw.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(Variables.chats));
            }
        }
        public static void LoadData()
        {
            Console.WriteLine("startedLoadData");
            if (!File.Exists("osuusers.txt"))
            {
                File.Create("osuusers.txt").Close();
            }
            else
            {
                using StreamReader sw = new StreamReader("osuusers.txt");
                List<osuUser> data = Newtonsoft.Json.JsonConvert.DeserializeObject<osuUser[]>(sw.ReadToEnd())?.ToList();
                Variables.osuUsers = data ?? new List<osuUser>();
            }
            if (!File.Exists("chats.txt"))
            {
                File.Create("chats.txt").Close();
            }
            else
            {
                using (StreamReader sw = new StreamReader("chats.txt"))
                {
                    List<Types.Chat> data = Newtonsoft.Json.JsonConvert.DeserializeObject<Types.Chat[]>(sw.ReadToEnd())?.ToList();
                    Variables.chats = data ?? new List<Chat>();
                }
            }
            Console.WriteLine("endedLoadData");
        }

        public static void SaveTimer()
        {
            var saveDataTimer = new System.Timers.Timer(TimeSpan.FromMinutes(30).TotalMilliseconds);
            saveDataTimer.Elapsed += (s, e) =>
            {
                try
                {
                    SaveData();
                    Console.WriteLine("saved");
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            };
            saveDataTimer.AutoReset = true;
            saveDataTimer.Start();
        }
    }
}
