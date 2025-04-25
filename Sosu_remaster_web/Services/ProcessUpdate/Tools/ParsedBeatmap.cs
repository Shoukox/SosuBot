using OppaiSharp;
using Beatmap = Sosu.osu.V1.Types.Beatmap;

namespace Sosu.Services.ProcessUpdate.Tools
{
    public class ParsedBeatmap
    {
        public long id;
        public bool isBeatmapset;

        public Mods mods;

        public double[]? acc100;
        public double[]? acc98;
        public double[]? acc96;
        public string? duration;

        public ParsedBeatmap(long id, bool isBeatmapset, Mods mods)
        {
            this.id = id;
            this.isBeatmapset = isBeatmapset;
            this.mods = mods;
        }
        public async Task<Beatmap> Parse()
        {
            Beatmap? beatmap = null;
            if (isBeatmapset)
                beatmap = await Variables.osuApi.GetBeatmapByBeatmapsetsIdAsync(id, 0, (int)(mods));
            else
                beatmap = await Variables.osuApi.GetBeatmapByBeatmapIdAsync(id, (int)(mods));

            this.acc100 = await PPCalc.ppCalc(int.Parse(beatmap.beatmap_id), 100, mods, 0, int.Parse(beatmap.max_combo));
            this.acc98 = await PPCalc.ppCalc(int.Parse(beatmap.beatmap_id), 98, mods, 0, int.Parse(beatmap.max_combo));
            this.acc96 = await PPCalc.ppCalc(int.Parse(beatmap.beatmap_id), 96, mods, 0, int.Parse(beatmap.max_combo));

            this.duration = $"{beatmap.hit_length() / 60}:{(beatmap.hit_length() % 60):00}";

            return beatmap;
        }
    }
}
