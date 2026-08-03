using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace SosuBot.Database.Models;

[PrimaryKey(nameof(ScoreId), nameof(ChatId))]
public class TrackedScoreDelivery
{
    public long ScoreId { get; set; }
    public long ChatId { get; set; }
    public bool IsAdminRecipient { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset AvailableAtUtc { get; set; }
    public DateTimeOffset? LockedUntilUtc { get; set; }

    [MaxLength(128)]
    public string? LockedBy { get; set; }

    public int Attempts { get; set; }
    public DateTimeOffset? SentAtUtc { get; set; }
    public DateTimeOffset? CancelledAtUtc { get; set; }
    public DateTimeOffset? FailedAtUtc { get; set; }

    [MaxLength(2000)]
    public string? LastError { get; set; }

    public virtual TrackedScoreEvent Event { get; set; } = null!;
}
