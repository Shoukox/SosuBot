using System.Text.Json.Serialization;

namespace SosuBot.Helpers.Types;

public record OpenAiFunctionCallParameters
{
    [JsonPropertyName("user_id")] public long? UserId { get; set; }

    [JsonPropertyName("score_type")] public string? ScoreType { get; set; }

    [JsonPropertyName("include_fails")] public int? IncludeFails { get; set; }

    [JsonPropertyName("mode")] public int? Mode { get; set; }

    [JsonPropertyName("limit")] public int? Limit { get; set; }

    [JsonPropertyName("count")] public int? Count { get; set; }

    [JsonPropertyName("country_code")] public string? CountryCode { get; set; }
}