using Newtonsoft.Json;

#nullable disable

namespace Sosu.osu.V1.Types
{
    public class User
    {
        [JsonProperty("username")]
        private string _username;
        public string username() => _username.Replace(" ", "_");

        public string profile_url() => $"http://osu.ppy.sh/u/{user_id}";

        public string user_id { get; set; }
        public string join_date { get; set; }

        [JsonProperty("count300")]
        private string _count300 { get; set; }
        public string count300() => _count300 ?? "0";

        [JsonProperty("count100")]
        private string _count100 { get; set; }
        public string count100() => _count100 ?? "0";

        [JsonProperty("count50")]
        private string _count50 { get; set; }
        public string count50() => _count50 ?? "0";

        [JsonProperty("playcount")]
        private string _playcount { get; set; }
        public string playcount() => _playcount ?? "0";

        [JsonProperty("ranked_score")]
        private string _ranked_score { get; set; }
        public string ranked_score() => _ranked_score ?? "0";

        [JsonProperty("total_score")]
        private string _total_score { get; set; }
        public string total_score() => _total_score ?? "0";

        [JsonProperty("pp_rank")]
        private string _pp_rank { get; set; }
        public string pp_rank() => _pp_rank ?? "0";

        [JsonProperty("level")]
        private string _level { get; set; }
        public string level() => _level ?? "0";

        [JsonProperty("pp_raw")]
        private string _pp_raw { get; set; }
        public string pp_raw() => _pp_raw ?? "0";

        [JsonProperty("accuracy")]
        private string _accuracy { get; set; }
        public string accuracy() => _accuracy ?? "0";

        [JsonProperty("count_rank_ss")]
        private string _count_rank_ss { get; set; }
        public string count_rank_ss() => _count_rank_ss ?? "0";

        [JsonProperty("count_rank_ssh")]
        private string _count_rank_ssh { get; set; }
        public string count_rank_ssh() => _count_rank_ssh ?? "0";

        [JsonProperty("count_rank_s")]
        private string _count_rank_s { get; set; }
        public string count_rank_s() => _count_rank_s ?? "0";

        [JsonProperty("count_rank_sh")]
        private string _count_rank_sh { get; set; }
        public string count_rank_sh() => _count_rank_sh ?? "0";

        [JsonProperty("count_rank_a")]
        private string _count_rank_a { get; set; }
        public string count_rank_a() => _count_rank_a ?? "0";

        [JsonProperty("country")]
        private string _country { get; set; }
        public string country() => _country ?? "-";

        [JsonProperty("total_seconds_played")]
        private string _total_seconds_played { get; set; }
        public string total_seconds_played() => _total_seconds_played ?? "0";
        public int playtime_hours() => int.Parse(total_seconds_played()) / 3600;

        [JsonProperty("pp_country_rank")]
        private string _pp_country_rank { get; set; }
        public string pp_country_rank() => _pp_country_rank ?? "0";

    }
}
