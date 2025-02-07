using OppaiSharp;

namespace Sosu.Services.ProcessUpdate.Tools
{
    public class PPCalc
    {
        public static async Task<double[]> ppCalc(long beatmap_id, double accuracy, Mods mods, int misses, int combo)
        {
            byte[] data;
            using (HttpClient hc = new HttpClient())
                data = await hc.GetByteArrayAsync($"https://osu.ppy.sh/osu/{beatmap_id}");
            MemoryStream stream = new MemoryStream(data, false);
            StreamReader reader = new StreamReader(stream);
            Beatmap beatmapp = Beatmap.Read(reader);
            PPv2 pp = new PPv2(new PPv2Parameters(beatmapp, accuracy / 100, misses, combo, mods));
            PPv2 ppifFc = new PPv2(new PPv2Parameters(beatmapp, accuracy / 100, 0, -1, mods));
            return new double[] { Math.Round(pp.Total), Math.Round(ppifFc.Total) };
        }
    }
}
