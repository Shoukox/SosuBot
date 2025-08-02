using System.Collections.Concurrent;

namespace SosuBot.Synchronization;

public class BotSynchronization
{
    private static readonly Lazy<BotSynchronization> instanceHolder = new(() => new BotSynchronization());

    private readonly ConcurrentDictionary<long, SemaphoreSlim> _syncDict;

    public BotSynchronization()
    {
        _syncDict = new ConcurrentDictionary<long, SemaphoreSlim>();
    }

    public static BotSynchronization Instance => instanceHolder.Value;

    public bool AddNewServerIfNeeded(long chatId)
    {
        return _syncDict.TryAdd(chatId, new SemaphoreSlim(1, 1));
    }

    public SemaphoreSlim GetSemaphoreSlim(long chatId)
    {
        return _syncDict.GetOrAdd(chatId, new SemaphoreSlim(1, 1));
    }
}