namespace SosuBot.Configuration;

public record BotConfiguration
{
    public required string Token { get; init; }
    public required string Username { get; init; }
    public required string? ApiServerUrl { get; init; } = "http://[2a03:4000:6:417a:1::108]:8081";
}