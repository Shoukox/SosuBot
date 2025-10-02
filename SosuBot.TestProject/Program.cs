using SosuBot.Graphics.ProfileCard;

namespace SosuBot.TestProject;

internal class Program
{
    private static void Main(string[] args)
    {
        var card = new OsuProfileCard(new OsuProfileCardInfo("Shoukko", 1231, 99.99,
            "https://a.ppy.sh/15319810?1743863734.jpeg"));
        card.CreateCard();
    }
}