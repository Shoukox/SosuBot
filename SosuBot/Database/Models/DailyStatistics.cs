using OsuApi.V2.Models;
using OsuApi.V2.Users.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SosuBot.Database.Models;

public record DailyStatistics
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    public DateTime DayOfStatistic { get; set; }
    public required string CountryCode { get; set; }
    public List<UserEntity> ActiveUsers { get; set; } = new();
    public List<int> BeatmapsPlayed { get; set; } = new();
    public List<ScoreEntity> Scores { get; set; } = new();
}


/// <summary>
/// JSON help types to convert from <see cref="User"/>
/// </summary>
public record UserEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    public User UserJson { get; set; } = null!;
}

/// <summary>
/// JSON help types to convert from <see cref="Score"/>
/// </summary>
public record ScoreEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }
    public Score ScoreJson { get; set; } = null!;
}