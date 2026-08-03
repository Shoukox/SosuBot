using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace SosuBot.Database.Models;

[PrimaryKey(nameof(Command), nameof(BucketStartUtc))]
[Index(nameof(BucketStartUtc))]
public class CommandUsageAggregate
{
    [MaxLength(64)]
    public required string Command { get; set; }

    public DateTimeOffset BucketStartUtc { get; set; }
    public long SuccessCount { get; set; }
    public long ErrorCount { get; set; }
    public long CancelledCount { get; set; }
    public long TotalDurationMilliseconds { get; set; }
}
