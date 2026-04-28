namespace SosuBot.Configuration;

public record RenderConfiguration
{
    public required string RenderUrl { get; set; }
    public required int ClientId { get; set; }
    public required string ClientSecret { get; set; }
}