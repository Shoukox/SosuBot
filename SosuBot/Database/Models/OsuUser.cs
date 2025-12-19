using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SosuBot.Helpers.Types;

// ReSharper disable InconsistentNaming

namespace SosuBot.Database.Models;

public record OsuUser
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public long TelegramId { get; set; }
    public required long OsuUserId { get; set; }

    [StringLength(maximumLength: 15, MinimumLength = 3)] 
    public required string OsuUsername { get; set; }
    public required Playmode OsuMode { get; set; }
    public double StdPPValue { get; set; }
    public double TaikoPPValue { get; set; }
    public double CatchPPValue { get; set; }
    public double ManiaPPValue { get; set; }
    public bool IsAdmin { get; set; }
}