using StackExchange.Redis;
using System.Text.Json;

namespace SosuBot.Caching;

public class RedisCaching(IConnectionMultiplexer mux)
{
    private readonly IDatabase _redis = mux.GetDatabase();
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };
    public async Task<T?> GetAsync<T>(string key) where T : class
    {
        var val = await _redis.StringGetAsync(key).ConfigureAwait(false);
        if (val.IsNullOrEmpty) return null;
        return JsonSerializer.Deserialize<T>(val!, _jsonOptions);
    }

    public Task<bool> SetAsync<T>(string key, T value, TimeSpan? ttl)
    {
        var payload = JsonSerializer.Serialize(value, _jsonOptions);
        return _redis.StringSetAsync(key, payload, ttl);
    }
}