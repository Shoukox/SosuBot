namespace SosuBot.Configuration;

public record OpenAiConfiguration
{
    public required string Token { get; init; }
    public required string Model { get; init; }
}