namespace SosuBot.Legacy;

public class osuUser
{
    public osuUser(long telegramId, string osuName, double pp)
    {
        this.telegramId = telegramId;
        this.osuName = osuName ?? $"{telegramId}";
        this.pp = pp;
    }

    public long telegramId { get; set; }
    public string osuName { get; set; }
    public double pp { get; set; }
}