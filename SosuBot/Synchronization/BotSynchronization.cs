using System.Collections.Concurrent;

namespace SosuBot.Synchronization;

public class BotSynchronization
{
    public static readonly Lazy<BotSynchronization> InstanceHolder = new(() => new BotSynchronization());

    private readonly ConcurrentDictionary<long, SemaphoreSlim> _syncDict;

    public BotSynchronization()
    {
        _syncDict = new ConcurrentDictionary<long, SemaphoreSlim>();
    }

    public static BotSynchronization Instance => InstanceHolder.Value;

    public bool AddNewServerIfNeeded(long chatId)
    {
        return _syncDict.TryAdd(chatId, new SemaphoreSlim(1, 1));
    }

    public SemaphoreSlim GetSemaphoreSlim(long chatId)
    {
        return _syncDict.GetOrAdd(chatId, new SemaphoreSlim(1, 1));
    }
}