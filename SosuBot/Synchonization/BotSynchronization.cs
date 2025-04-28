using System.Collections.Concurrent;

namespace SosuBot.Synchronization
{
    public class BotSynchronization
    {
        private static readonly Lazy<BotSynchronization> instanceHolder = new Lazy<BotSynchronization>(() => new BotSynchronization());
        public static BotSynchronization Instance => instanceHolder.Value;

        private ConcurrentDictionary<long, SemaphoreSlim> _syncDict;

        public BotSynchronization()
        {
            _syncDict = new ConcurrentDictionary<long, SemaphoreSlim>();
        }

        public bool AddNewServerIfNeeded(long chatId)
        {
            return _syncDict.TryAdd(chatId, new SemaphoreSlim(1, 1));
        }

        public SemaphoreSlim GetSemaphoreSlim(long chatId)
        {
            return _syncDict.GetOrAdd(chatId, new SemaphoreSlim(1, 1));
        }
    }
}
