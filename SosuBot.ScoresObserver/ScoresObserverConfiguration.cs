namespace SosuBot.ScoresObserver;

public sealed class ScoresObserverConfiguration
{
    public bool CreateDeliveries { get; set; } = true;
    public int ScoresLimit { get; set; } = 50;
    public int LeaderboardPlayers { get; set; } = 50;
    public TimeSpan UserPollDelay { get; set; } = TimeSpan.FromSeconds(5);
    public TimeSpan EmptyObserverDelay { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan ErrorDelay { get; set; } = TimeSpan.FromSeconds(10);
    public TimeSpan LeaderboardRefreshInterval { get; set; } = TimeSpan.FromHours(6);
}
