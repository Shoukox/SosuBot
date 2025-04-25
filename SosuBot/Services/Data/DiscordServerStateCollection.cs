using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SosuBot.Services.Data
{
    public class DiscordServerStateCollection : IDiscordServerStateCollection
    {
        //private readonly ConcurrentDictionary<ulong, DiscordServerState> _states = new();

        //public DiscordServerState GetOrCreateState(ulong guildId) =>
        //    _states.GetOrAdd(guildId, _ => new DiscordServerState());

        //public bool TryGetState(ulong guildId, out DiscordServerState state) =>
        //    _states.TryGetValue(guildId, out state!);

        //public void RemoveState(ulong guildId) =>
        //    _states.TryRemove(guildId, out _);
    }
}
