using Newtonsoft.Json;

namespace Sosu.osu.V1.Types
{
    public class Beatmap
    {
        public string beatmapset_id { get; set; }
        public string beatmap_id { get; set; }

        [JsonProperty("total_length")]
        private string _total_length { get; set; }
        public int total_length()
        {
            string m = $"{mods()}";
            double len = double.Parse(_total_length);
            if (m.Contains(Enums.Mods.DoubleTime.ToString()) || m.Contains(Enums.Mods.Nightcore.ToString()))
            {
                len = int.Parse(_total_length) / 1.5f;
            }
            if (m.Contains(Enums.Mods.HalfTime.ToString()))
            {
                len = int.Parse(_total_length) * 1.25f;
            }
            return System.Convert.ToInt32(len);
        }

        [JsonProperty("hit_length")]
        private string _hit_length { get; set; }
        public int hit_length()
        {
            string m = $"{mods()}";
            double len = double.Parse(_hit_length);
            if (m.Contains(Enums.Mods.DoubleTime.ToString()) || m.Contains(Enums.Mods.Nightcore.ToString()))
            {
                len = int.Parse(_hit_length) / 1.5f;
            }
            else if (m.Contains(Enums.Mods.HalfTime.ToString()))
            {
                len = int.Parse(_hit_length) * 1.25f;
            }
            return System.Convert.ToInt32(len);
        }

        public string version { get; set; }
        public string file_md5 { get; set; }

        [JsonProperty("diff_size")]
        private string _diff_size { get; set; }
        public double diff_size()
        {
            string m = $"{mods()}";
            double cs = double.Parse(_diff_size);
            if (m.Contains(Enums.Mods.Easy.ToString()))
            {
                cs = cs / 2;
            }
            else if (m.Contains(Enums.Mods.Hardrock.ToString()))
            {
                cs = System.Math.Clamp(cs * 1.3, 0, 10);
            }
            return System.Math.Round(cs, 1);
        }

        [JsonProperty("diff_overall")]
        private string _diff_overall { get; set; }
        public double diff_overall()
        {
            string m = $"{mods()}";
            double od = double.Parse(_diff_overall);
            if (m.Contains(Enums.Mods.Easy.ToString()))
            {
                od = od / 2;
            }
            else if (m.Contains(Enums.Mods.Hardrock.ToString()))
            {
                od = System.Math.Clamp(od * 1.4, 0, 10);
            }
            return System.Math.Round(od, 1);
        }

        [JsonProperty("diff_approach")]
        private string _diff_approach { get; set; }
        public double diff_approach()
        {
            string m = $"{mods()}";
            double ar = double.Parse(_diff_approach);
            if (m.Contains(Enums.Mods.Easy.ToString()))
            {
                ar = ar / 2;
            }
            else if (m.Contains(Enums.Mods.Hardrock.ToString()))
            {
                ar = System.Math.Clamp(ar * 1.4, 0, 10);
            }
            if (m.Contains(Enums.Mods.DoubleTime.ToString()) || m.Contains(Enums.Mods.Nightcore.ToString()))
            {
                ar = (ar * 2 + 13) / 3;
            }
            else if (m.Contains(Enums.Mods.HalfTime.ToString()))
            {
                ar = (ar * 4 - 13) / 3;
            }
            return System.Math.Round(ar, 1);
        }

        [JsonProperty("diff_drain")]
        private string _diff_drain { get; set; }
        public double diff_drain()
        {
            string m = $"{mods()}";
            double hp = double.Parse(_diff_drain);
            if (m.Contains(Enums.Mods.Easy.ToString()))
            {
                hp = hp / 2;
            }
            else if (m.Contains(Enums.Mods.Hardrock.ToString()))
            {
                hp = System.Math.Clamp(hp * 1.4, 0, 10);
            }
            return System.Math.Round(hp, 1);
        }

        [JsonProperty("mode")]
        public string _mode { get; set; }
        public Enums.Mods mods() => Variables.osuApi.CalculateModsMods(int.Parse(_mode));

        public string count_normal { get; set; }
        public string count_slider { get; set; }
        public string count_spinner { get; set; }
        public string submit_date { get; set; }
        public string approved_date { get; set; }
        public string last_update { get; set; }
        public string artist { get; set; }
        public string artist_unicode { get; set; }
        public string title { get; set; }
        public string title_unicode { get; set; }
        public string creator { get; set; }
        public string creator_id { get; set; }

        [JsonProperty("bpm")]
        private string _bpm { get; set; }
        public double bpm()
        {
            string m = $"{mods()}";
            double bpm = double.Parse(_bpm);
            if (m.Contains(Enums.Mods.DoubleTime.ToString()) || m.Contains(Enums.Mods.Nightcore.ToString()))
            {
                bpm = bpm * 1.5;
            }
            else if (m.Contains(Enums.Mods.HalfTime.ToString()))
            {
                bpm = bpm * 0.75;
            }
            return System.Convert.ToDouble(bpm);
        }

        public string source { get; set; }
        public string tags { get; set; }
        public string genre_id { get; set; }
        public string language_id { get; set; }
        public string favourite_count { get; set; }
        public string rating { get; set; }
        public string storyboard { get; set; }
        public string video { get; set; }
        public string download_unavailable { get; set; }
        public string audio_unavailable { get; set; }
        public string playcount { get; set; }
        public string passcount { get; set; }
        public string packs { get; set; }
        public string max_combo { get; set; }
        public string diff_aim { get; set; }
        public string diff_speed { get; set; }
        public string difficultyrating { get; set; }
        public int countobjects() => int.Parse(count_normal) + int.Parse(count_slider) + int.Parse(count_spinner);

        [JsonProperty("approved")]
        private string approved { get; set; }
        public string GetApproved()
        {
            return approved switch
            {
                "-2" => "Graveyard",
                "-1" => "WIP",
                "0" => "Pending",
                "1" => "Ranked",
                "2" => "Approved",
                "3" => "Qualified",
                "4" => "Loved",
                _ => "unbekannt"
            };
        }
        public void ParseHTML()
        {
            version = version.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
            title = title.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
            artist = artist.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
        }

    }
}
