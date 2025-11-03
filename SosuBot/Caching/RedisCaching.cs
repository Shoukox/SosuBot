using StackExchange.Redis;
using System.Text.Json;

namespace SosuBot.Caching;

public class RedisCaching(IConnectionMultiplexer mux)
{
    private readonly IDatabase _redis = mux.GetDatabase();
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static string KeyFor(string obj, string key) => $"{obj}:{key}";

    public async Task<T?> GetAsync<T>(string nameOfObject, string key) where T : class
    {
        var val = await _redis.StringGetAsync(KeyFor(nameOfObject, key)).ConfigureAwait(false);
        if (val.IsNullOrEmpty) return null;
        return JsonSerializer.Deserialize<T>(val!, _jsonOptions);
    }

    public Task<bool> SetAsync<T>(string key, string obj, T value, TimeSpan ttl)
    {
        var payload = JsonSerializer.Serialize(value, _jsonOptions);
        return _redis.StringSetAsync(KeyFor(key, obj), payload, ttl);
    }
}