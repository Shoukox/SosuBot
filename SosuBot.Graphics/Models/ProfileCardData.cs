namespace SosuBot.Graphics.Models;

public sealed record ProfileCardData
{
    public required string Username { get; init; }
    public required OsuGameMode Mode { get; init; }
    public required PlayerSkills Skills { get; init; }
    public byte[]? Avatar { get; init; }
}
