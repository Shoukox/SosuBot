using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SosuBot.Database.Models;

public record TelegramChat
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public long ChatId { get; init; }

    public List<long>? ChatMembers { get; set; }
    public List<long>? ExcludeFromChatstats { get; set; }
    public long? LastBeatmapId { get; set; }
}
