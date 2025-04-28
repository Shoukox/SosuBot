namespace SosuBot;

public record BotConfiguration
{
    public required string Token { get; init; }
    public required string Username { get; init; }
}