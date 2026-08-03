using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SosuBot.Database.Models;

public class TrackedPlayerCheckpoint
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int PlayerId { get; set; }

    public int? Mode { get; set; }
    public List<long> BestScoreIds { get; set; } = [];
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public bool IsActive { get; set; } = true;
}
