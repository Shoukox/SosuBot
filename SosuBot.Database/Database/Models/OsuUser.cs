using SosuBot.Database.Database.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

// ReSharper disable InconsistentNaming

namespace SosuBot.Database.Models;

public record OsuUser
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public long TelegramId { get; set; }
    public long OsuUserId { get; set; } = -1;

    [StringLength(maximumLength: 15, MinimumLength = 3)]
    public string OsuUsername { get; set; } = "";
    public Playmode OsuMode { get; set; } = Playmode.Osu;
    public double StdPPValue { get; set; }
    public double TaikoPPValue { get; set; }
    public double CatchPPValue { get; set; }
    public double ManiaPPValue { get; set; }
    public bool IsAdmin { get; set; }
    public DanserConfiguration RenderSettings { get; set; } = new();
}