using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SosuBot.Helpers.Types;

// ReSharper disable InconsistentNaming

namespace SosuBot.Database.Models;

public class OsuUser
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public long TelegramId { get; set; }

    public required long OsuUserId { get; set; }

    [StringLength(15)] public required string OsuUsername { get; set; }

    public required Playmode OsuMode { get; set; }

    public double StdPPValue { get; set; }
    public double TaikoPPValue { get; set; }
    public double CatchPPValue { get; set; }
    public double ManiaPPValue { get; set; }

    public bool IsAdmin { get; set; }
}