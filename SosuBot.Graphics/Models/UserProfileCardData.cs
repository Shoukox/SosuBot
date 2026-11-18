namespace SosuBot.Graphics.Models;

/// <summary>What the /userp picture shows: a player's profile in one mode.</summary>
public sealed class UserProfileCardData
{
    public required string Username { get; init; }
    public string? CountryCode { get; init; }
    public OsuGameMode Mode { get; init; }

    public byte[]? Avatar { get; init; }

    /// <summary>The profile banner, shown blurred behind the card; the avatar's colours stand in without it.</summary>
    public byte[]? Banner { get; init; }

    public int? GlobalRank { get; init; }
    public int? CountryRank { get; init; }
    public double? Pp { get; init; }
    public double? AccuracyPercent { get; init; }
    public int? PlayCount { get; init; }
    public long? PlayTimeHours { get; init; }
    public int? MaximumCombo { get; init; }
    public int? Level { get; init; }
    public int? LevelProgress { get; init; }

    /// <summary>The change since the player's last check, like "(+12.30)"; empty when unknown.</summary>
    public string PpDifference { get; init; } = "";

    public string AccuracyDifference { get; init; } = "";
    public string PlayTimeDifference { get; init; } = "";

    public int? SilverSs { get; init; }
    public int? Ss { get; init; }
    public int? SilverS { get; init; }
    public int? S { get; init; }
    public int? A { get; init; }

    /// <summary>The registration date, already formatted.</summary>
    public string? Registered { get; init; }

    /// <summary>Unlocked achievements out of all of them, like "120/344".</summary>
    public string? Achievements { get; init; }
}
