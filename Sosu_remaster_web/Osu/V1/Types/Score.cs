namespace Sosu.osu.V1.Types
{
    public class Score
    {
        public string beatmap_id { get; set; } //null in /score
        public string score_id { get; set; }
        public string score { get; set; }
        public string maxcombo { get; set; }
        public string count50 { get; set; }
        public string count100 { get; set; }
        public string count300 { get; set; }
        public string countmiss { get; set; }
        public string countkatu { get; set; }
        public string countgeki { get; set; }
        public string perfect { get; set; }
        public string enabled_mods { get; set; }
        public string user_id { get; set; }
        public string date { get; set; }
        public string rank { get; set; }
        public string pp { get; set; }
        public string replay_available { get; set; }
        public int countobjects() => int.Parse(countmiss) + int.Parse(count300) + int.Parse(count100) + int.Parse(count50);
        public double accuracy()
        {
            return (50 * double.Parse(count50) + 100 * double.Parse(count100) + 300 * double.Parse(count300)) / (300 * (double.Parse(countmiss) + double.Parse(count50) + double.Parse(count100) + double.Parse(count300))) * 100;
        }
        public double completion(int beatmap_objects)
        {
            return (countobjects() / (double)beatmap_objects) * 100.0;
        }
    }
}
