namespace SosuBot;

public record BotConfiguration
{
    public required string Token { get; init; }
    public required string Username { get; init; }
    public required int ApiId { get; init; }
    public required string ApiHash { get; init; }
}