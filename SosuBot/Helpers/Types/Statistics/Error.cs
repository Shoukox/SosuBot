namespace SosuBot.Helpers.Types.Statistics;

public record Error(ErrorCode Code)
{
    public string? Description { get; set; }
}