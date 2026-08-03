using System.ComponentModel.DataAnnotations;

namespace SosuBot.Database.Models;

public class ScoreFeedCheckpoint
{
    [Key]
    [MaxLength(32)]
    public string Source { get; set; } = string.Empty;

    public string? Cursor { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
