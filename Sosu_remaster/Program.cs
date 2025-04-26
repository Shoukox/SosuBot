using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Sosu_remaster
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // dotnet build ONLY FROM PROJECT DIRECTORY! (for textdatabase)
            // get config from json
            string path = "appsettings.json";
            if (!File.Exists(path)) throw new Exception("You should firstly create an appsettings.json and pass your client_id and client_secret into it!");
            var configuration = JsonSerializer.Deserialize<BotConfiguration>(File.ReadAllText(path).Trim());
            if (configuration == null) throw new Exception("Bad appsettings.json");

            string token = configuration.Token;
            SosuInstance si = new SosuInstance(token);
            _ = si.Start();

            while (true) { Console.ReadLine(); }
        }
    }
}
