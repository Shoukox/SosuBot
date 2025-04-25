namespace Sosu.Types
{
    public class PerformanceCalculatorModell
    {
        public Score score { get; set; }
        public PerformanceAttributes performance_attributes { get; set; }
        public DifficultyAttributes difficulty_attributes { get; set; }
    }

    public class Mod
    {
        public string acronym { get; set; }
        public Settings settings { get; set; }
    }

    public class Settings
    {
    }

    public class Statistics
    {
        public int great { get; set; }
        public int ok { get; set; }
        public int meh { get; set; }
        public int miss { get; set; }
    }

    public class Score
    {
        public int ruleset_id { get; set; }
        public int beatmap_id { get; set; }
        public string beatmap { get; set; }
        public List<Mod> mods { get; set; }
        public int total_score { get; set; }
        public double accuracy { get; set; }
        public int combo { get; set; }
        public Statistics statistics { get; set; }
    }

    public class PerformanceAttributes
    {
        public double aim { get; set; }
        public double speed { get; set; }
        public double accuracy { get; set; }
        public double flashlight { get; set; }
        public double effective_miss_count { get; set; }
        public double pp { get; set; }
    }

    public class DifficultyAttributes
    {
        public double star_rating { get; set; }
        public int max_combo { get; set; }
        public double aim_difficulty { get; set; }
        public double speed_difficulty { get; set; }
        public double flashlight_difficulty { get; set; }
        public double slider_factor { get; set; }
        public double approach_rate { get; set; }
        public double overall_difficulty { get; set; }
    }
}
