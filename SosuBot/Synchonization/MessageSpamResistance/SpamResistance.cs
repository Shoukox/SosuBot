using osu.Game.Rulesets.Osu.Difficulty.Evaluators;
using SosuBot.Synchronization;
using System.Collections.Concurrent;
using Telegram.Bot.Types;

namespace SosuBot.Synchonization.MessageSpamResistance
{
    public class SpamResistance
    {
        private static readonly Lazy<SpamResistance> instanceHolder = new Lazy<SpamResistance>(() => new SpamResistance());
        public static SpamResistance Instance => instanceHolder.Value;

        private readonly ConcurrentDictionary<long, MessagingUser> _usersDict;
        private readonly int _maxMessagesPerInterval = 4;

        public static readonly TimeSpan Interval = TimeSpan.FromSeconds(3);
        public static readonly TimeSpan BlockInterval = TimeSpan.FromSeconds(15);

        public SpamResistance()
        {
            _usersDict = new ConcurrentDictionary<long, MessagingUser>();
        }

        public async Task<(bool canSend, bool sendWarning)> CanSendMessage(long userId, DateTime newMessageSent)
        {
            // Get semaphore slim for synchronization
            var semaphoreSlim = BotSynchronization.Instance.GetSemaphoreSlim(userId);
            await semaphoreSlim.WaitAsync();

            // If already blocked, then return instantly
            var messagingUser = _usersDict.GetOrAdd(userId, _ => AddNew(userId));
            if (IsBlocked(messagingUser))
            {
                semaphoreSlim.Release();
                return (false, false);
            }

            var dateTimeNow = DateTime.UtcNow;

            // Ban if necessary
            bool canSend = true;
            if (messagingUser.MessagesQueue.Take(_maxMessagesPerInterval) is IEnumerable<DateTime> dateTimes && dateTimes.Count() == _maxMessagesPerInterval && dateTimes.All(m => dateTimeNow - m < Interval))
            {
                messagingUser.BlockedUntil = dateTimeNow.Add(BlockInterval);
                messagingUser.WarningMessageSent = messagingUser.WarningMessageSent ? false : true;
                canSend = false;
            }
            else
            {
                messagingUser.WarningMessageSent = false;
                canSend = true;
            }

            // Update our user
            _usersDict.AddOrUpdate(userId,
                addValue: AddNew(userId),
                updateValueFactory: (userId, messagingUser) => UpdateValue(userId, messagingUser, newMessageSent));

            // Release semaphore and return result
            semaphoreSlim.Release();
            return (canSend, messagingUser.WarningMessageSent);
        }

        private bool IsBlocked(MessagingUser messagingUser) => DateTime.UtcNow < messagingUser.BlockedUntil;

        private MessagingUser AddNew(long userId)
        {
            return new MessagingUser()
            {
                BlockedUntil = DateTime.MinValue,
                MessagesQueue = new Queue<DateTime>(),
                TelegramUserId = userId,
                WarningMessageSent = false
            };
        }

        private MessagingUser UpdateValue(long userId, MessagingUser messagingUser, DateTime messageSent)
        {
            var dateTimeNow = DateTime.UtcNow;
            var queue = messagingUser.MessagesQueue;
            while (queue.Count > 0 && dateTimeNow - queue.Peek() > Interval) queue.Dequeue();
            queue.Enqueue(messageSent.ToUniversalTime());

            return new MessagingUser()
            {
                BlockedUntil = messagingUser.BlockedUntil,
                MessagesQueue = queue,
                TelegramUserId = userId,
                WarningMessageSent = messagingUser.WarningMessageSent
            };
        }
    }
}
