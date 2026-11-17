namespace SosuBot.Configuration;

public record OpenAiConfiguration
{
    public required string Token { get; init; }

    /// <summary>A model with function calling in the Responses API, e.g. gpt-4o-mini or gpt-5-mini.</summary>
    public required string Model { get; init; }

    /// <summary>
    /// Sampling temperature. Sent only when set: reasoning models (gpt-5 family) reject it.
    /// </summary>
    public float? Temperature { get; init; }

    /// <summary>
    /// Reasoning effort for reasoning models ("none", "minimal", "low", "medium", ...). Sent only when set;
    /// non-reasoning models such as gpt-4o-mini reject it. Lower effort answers faster.
    /// </summary>
    public string? Reasoning { get; init; }

    /// <summary>Upper bound on one reply including reasoning tokens.</summary>
    public int MaxOutputTokens { get; init; } = 1_500;
}
