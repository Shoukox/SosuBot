﻿using Sosu.Types;

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
            if (!File.Exists("osuusers.txt"))
                File.Create("osuusers.txt").Close();
            if (!File.Exists("chats.txt"))
                File.Create("chats.txt").Close();
            Console.WriteLine("startedLoadData");
            using (StreamReader sw = new StreamReader("osuusers.txt"))
            {
                List<osuUser> data = Newtonsoft.Json.JsonConvert.DeserializeObject<osuUser[]>(sw.ReadToEnd()).ToList();
                Console.WriteLine(data);
                Variables.osuUsers = data ?? new List<osuUser>();
            }
            using (StreamReader sw = new StreamReader("chats.txt"))
            {
                List<Types.Chat> data = Newtonsoft.Json.JsonConvert.DeserializeObject<Types.Chat[]>(sw.ReadToEnd()).ToList();
                Console.WriteLine(data);
                Variables.chats =  data ?? new List<Chat>();
            }
            Console.WriteLine("endedLoadData");
        }
    }
}
