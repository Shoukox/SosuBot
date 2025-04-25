using Newtonsoft.Json;
using Sosu.osu.V1.Enums;
using Sosu.osu.V1.Types;

namespace Sosu.osu.V1
{
    public class osuApi
    {
        public string token { get; set; }

        string[][] modsValues = new string[][]
        {
                new string[] {"0","None"},
                new string[] {"1","NF"},
                new string[] {"2","EZ"},
                new string[] {"4","TD"},
                new string[] {"8","HD"},
                new string[] {"16","HR"},
                new string[] {"32","SD"},
                new string[] {"64","DT"},
                new string[] {"128","RX"},
                new string[] {"256","HT"},
                new string[] {"576","NC"},
                new string[] {"1024","FL"},
                new string[] {"16416", "PF"}
        };
        public osuApi(string token)
        {
            this.token = token;
        }
        public string GetGameMode(int modeId, bool capitalize = false)
        {
            if (modeId >= 4)
                modeId = 0;
            string mode = ((GameMode)modeId).ToString();

            if (capitalize)
                mode = mode.Substring(0, 1).ToUpper() + mode.Substring(1, mode.Length - 2);

            return mode;
        }
        public Mods getMod(Mods mods, ref string mod)
        {
            return mod switch
            {
                "NF" => mods | Mods.NoFail,
                "EZ" => mods | Mods.Easy,
                "TD" => mods | Mods.TouchDevice,
                "SD" => mods | Mods.SuddenDeath,
                "HD" => mods | Mods.Hidden,
                "HR" => mods | Mods.Hardrock,
                "NC" => mods | Mods.Nightcore,
                "DT" => mods | Mods.DoubleTime,
                "HT" => mods | Mods.HalfTime,
                "PF" => mods | Mods.Perfect,
                "FL" => mods | Mods.Flashlight,
                _ => throw new System.Exception("No match for this mod")
            };
        }
        public Mods CalculateModsMods(int enabled_mods)
        {
            Mods mods = new Mods();
            string curmods = "";
            for (int i = modsValues.Length - 1; i >= 0; i--)
            {
                if (enabled_mods == 0) break;
                if (int.Parse(modsValues[i][0]) <= enabled_mods)
                {
                    curmods += modsValues[i][1];
                    string mod = curmods.Substring(curmods.Length - 2, 2);
                    mods = getMod(mods, ref mod);
                    enabled_mods -= int.Parse(modsValues[i][0]);
                }
            }
            return mods;
        }
        public int CalculateModsForBeatmap(int enabled_mods)
        {
            int curmods = 0;
            for (int i = modsValues.Length - 1; i >= 0; i--)
            {
                if (enabled_mods == 0) break;
                if (int.Parse(modsValues[i][0]) <= enabled_mods)
                {
                    if (modsValues[i][1] == "HR" || modsValues[i][1] == "DT" || modsValues[i][1] == "NC" || modsValues[i][1] == "EZ" || modsValues[i][1] == "HT")
                        curmods += int.Parse(modsValues[i][0]);
                    enabled_mods -= int.Parse(modsValues[i][0]);
                }
            }
            return curmods;
        }

        /// <param name="type">"string" or "id"</param>
        /// <returns></returns>
        public async Task<User>? GetUserInfoByNameAsync(string name, int mode = 0, string type = "string")
        {
            using (HttpClient hc = new HttpClient())
            {
                string doc = await hc.GetStringAsync($"https://osu.ppy.sh/api/get_user?k={token}&u={name}&m={mode}&type={type}");
                if (doc.Length == 2) return null;
                doc = doc.Remove(0, 1).Remove(doc.IndexOf(",\"events\"") - 1, doc.Length - doc.IndexOf(",\"events\"")).Replace("\"", "'") + "}";
                return JsonConvert.DeserializeObject<User>(doc);
            }
        }
        public async Task<Score[]>? GetUserBestByNameAsync(string name, int count = 5, int mode = 0)
        {
            Score[] returndata;
            using (HttpClient hc = new HttpClient())
            {
                string doc = await hc.GetStringAsync($"https://osu.ppy.sh/api/get_user_best?k={token}&u={name}&limit={count}&m={mode}");
                if (doc.Length == 2) return null;
                returndata = JsonConvert.DeserializeObject<Score[]>(doc);
            }
            return returndata;
        }
        public async Task<Score[]>? GetRecentScoresByNameAsync(string name, int count = 1)
        {
            Score[] returndata;
            if (count > 5) count = 5;
            using (HttpClient hc = new HttpClient())
            {
                string doc = await hc.GetStringAsync($"https://osu.ppy.sh/api/get_user_recent?k={token}&u={name}&limit={count}");
                if (doc.Length == 2) return null;
                returndata = JsonConvert.DeserializeObject<Score[]>(doc);
            }
            return returndata;
        }
        public async Task<Beatmap>? GetBeatmapByBeatmapIdAsync(long beatmap_id, int mods = 0)
        {
            using (HttpClient hc = new HttpClient())
            {
                mods = CalculateModsForBeatmap(mods);
                string doc = await hc.GetStringAsync($"https://osu.ppy.sh/api/get_beatmaps?k={token}&b={beatmap_id}&mods={mods}");
                if (doc.Length == 2) return null;
                var data = JsonConvert.DeserializeObject<Beatmap[]>(doc)[0];
                data._mode = mods.ToString();
                return data;
            }
        }
        /// <param name="diffIndex">0 - hardest, n - easiest</param>
        /// <returns></returns>
        public async Task<Beatmap>? GetBeatmapByBeatmapsetsIdAsync(long beatmapsets_id, int diffIndex = 0, int mods = 0)
        {
            using (HttpClient hc = new HttpClient())
            {
                mods = CalculateModsForBeatmap(mods);
                string doc = await hc.GetStringAsync($"https://osu.ppy.sh/api/get_beatmaps?k={token}&s={beatmapsets_id}&mods={mods}");
                if (doc.Length == 2) return null;
                var data = JsonConvert.DeserializeObject<Beatmap[]>(doc).OrderByDescending(m => double.Parse(m.difficultyrating)).ToArray()[diffIndex];
                data._mode = mods.ToString();
                return data;
            }
        }
        public async Task<Score[]>? GetScoresOnMapByName(string name, long beatmap_id)
        {
            Score[]? returndata = null;
            using (HttpClient hc = new HttpClient())
            {
                string doc = await hc.GetStringAsync($"https://osu.ppy.sh/api/get_scores?k={token}&u={name}&b={beatmap_id}");
                if (doc.Length == 2) return null;
                Console.WriteLine(doc);
                returndata = JsonConvert.DeserializeObject<Score[]>(doc);
            }
            return returndata;
        }
    }

}
