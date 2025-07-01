namespace SosuBot.Synchonization.MessageSpamResistance
{
    public record MessagingUser
    {
        public long TelegramUserId { get; set; }

        public DateTime BlockedUntil { get; set; }
        public bool WarningMessageSent { get; set; }
        public required Queue<DateTime> MessagesQueue { get; set; }
    }
}
