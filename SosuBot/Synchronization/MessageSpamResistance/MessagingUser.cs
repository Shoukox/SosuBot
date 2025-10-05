using System.Collections.Concurrent;

namespace SosuBot.Synchronization.MessageSpamResistance;

public record MessagingUser
{
    public long TelegramUserId { get; set; }

    public DateTime BlockedUntil { get; set; }
    public bool WarningMessageSent { get; set; }
    public required ConcurrentQueue<DateTime> MessagesQueue { get; set; }
}