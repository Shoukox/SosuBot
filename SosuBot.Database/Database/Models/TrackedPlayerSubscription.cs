using Microsoft.EntityFrameworkCore;

namespace SosuBot.Database.Models;

[PrimaryKey(nameof(ChatId), nameof(PlayerId))]
public class TrackedPlayerSubscription
{
    public long ChatId { get; set; }
    public int PlayerId { get; set; }
    public DateTimeOffset StartedAtUtc { get; set; }

    public virtual TelegramChat Chat { get; set; } = null!;
}
