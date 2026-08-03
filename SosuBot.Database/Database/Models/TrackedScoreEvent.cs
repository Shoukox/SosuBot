using OsuApi.BanchoV2.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SosuBot.Database.Models;

public class TrackedScoreEvent
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public long ScoreId { get; set; }

    public int PlayerId { get; set; }
    public Score ScoreJson { get; set; } = null!;
    public DateTimeOffset OccurredAtUtc { get; set; }
    public DateTimeOffset DetectedAtUtc { get; set; }

    public virtual List<TrackedScoreDelivery> Deliveries { get; set; } = [];
}
