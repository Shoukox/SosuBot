namespace Sosu.Types
{
    public class osuUser
    {
        public long telegramId { get; set; }
        public string osuName { get; set; }
        public double pp { get; set; }

        public osuUser(long telegramId, string osuName, double pp)
        {
            this.telegramId = telegramId;
            this.osuName = osuName ?? $"{telegramId}";
            this.pp = pp;
        }
    }
}
