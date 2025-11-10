using System.Collections.Concurrent;

namespace SosuBot.Synchronization.MessageSpamResistance;

public class SpamResistance
{
    public static readonly Lazy<SpamResistance> InstanceHolder = new(() => new SpamResistance());

    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan BlockInterval = TimeSpan.FromSeconds(15);
    private readonly int _maxMessagesPerInterval = 7;

    private readonly ConcurrentDictionary<long, MessagingUser> _usersDict;

    public SpamResistance()
    {
        _usersDict = new ConcurrentDictionary<long, MessagingUser>();
    }

    public static SpamResistance Instance => InstanceHolder.Value;

    public async Task<(bool canSend, bool sendWarning)> CanSendMessage(long userId, DateTime newMessageSent)
    {
        // Get semaphore slim for synchronization
        var semaphoreSlim = BotSynchronization.Instance.GetSemaphoreSlim(userId);
        await semaphoreSlim.WaitAsync();

        try
        {
            // If already blocked, then return instantly
            var messagingUser = _usersDict.GetOrAdd(userId, _ => AddNew(userId));
            if (IsBlocked(messagingUser)) return (false, false);

            var dateTimeNow = DateTime.Now;

            // Ban if necessary
            bool canSend;
            if (messagingUser.MessagesQueue.Take(_maxMessagesPerInterval) is { } dateTimesEnumerable
                && dateTimesEnumerable.ToArray() is { } dateTimes
                && dateTimes.Length == _maxMessagesPerInterval && dateTimes.All(m => dateTimeNow - m < Interval))
            {
                messagingUser.BlockedUntil = dateTimeNow.Add(BlockInterval);
                messagingUser.WarningMessageSent = !messagingUser.WarningMessageSent;
                canSend = false;
            }
            else
            {
                messagingUser.WarningMessageSent = false;
                canSend = true;
            }

            // Update our user
            _usersDict.AddOrUpdate(userId, AddNew(userId),
                (uId, mUser) => UpdateValue(uId, mUser, newMessageSent));

            return (canSend, messagingUser.WarningMessageSent);
        }
        finally
        {
            semaphoreSlim.Release();
        }
    }

    private bool IsBlocked(MessagingUser messagingUser)
    {
        return DateTime.UtcNow < messagingUser.BlockedUntil;
    }

    private MessagingUser AddNew(long userId)
    {
        return new MessagingUser
        {
            BlockedUntil = DateTime.MinValue,
            MessagesQueue = new ConcurrentQueue<DateTime>(),
            TelegramUserId = userId,
            WarningMessageSent = false
        };
    }

    private MessagingUser UpdateValue(long userId, MessagingUser messagingUser, DateTime messageSent)
    {
        var dateTimeNow = DateTime.UtcNow;
        var queue = messagingUser.MessagesQueue;

        queue.TryPeek(out var peekQueueDateTime);

        while (queue.Count > 0 && dateTimeNow - peekQueueDateTime > Interval) queue.TryDequeue(out _);
        queue.Enqueue(messageSent); // It's in our current timezone

        return new MessagingUser
        {
            BlockedUntil = messagingUser.BlockedUntil,
            MessagesQueue = queue,
            TelegramUserId = userId,
            WarningMessageSent = messagingUser.WarningMessageSent
        };
    }
}