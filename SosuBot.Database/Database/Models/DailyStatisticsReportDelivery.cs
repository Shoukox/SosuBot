using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace SosuBot.Database.Models;

/// <summary>
/// Durable outbox entry for one playmode of a completed daily statistic.
/// The composite key makes report creation idempotent across observer restarts.
/// </summary>
[PrimaryKey(nameof(DailyStatisticsId), nameof(Mode))]
public class DailyStatisticsReportDelivery
{
    public int DailyStatisticsId { get; set; }
    public Playmode Mode { get; set; }
    public long ChatId { get; set; }
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

    public virtual DailyStatistics DailyStatistics { get; set; } = null!;
}
