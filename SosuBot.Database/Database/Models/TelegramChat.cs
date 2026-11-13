using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SosuBot.Database.Models;

public record TelegramChat
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public long ChatId { get; init; }
    public List<long>? ChatMembers { get; set; }
    public List<long>? ExcludeFromChatstats { get; set; }
    public int? LastBeatmapId { get; set; }
    public string LanguageCode { get; set; } = "ru";
    public List<int>? TrackedPlayers { get; set; }
    public List<int>? BlockedTopicIds { get; set; }
    public bool CommandRestrictionsEnabled { get; set; }
    public int CommandCooldownSeconds { get; set; }
    public List<string>? DisabledCommands { get; set; }
    public bool IgnoreMedia { get; set; }
}