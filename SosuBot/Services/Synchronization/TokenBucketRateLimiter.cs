using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace SosuBot.Services.Synchronization
{
    public class TokenBucketRateLimiter
    {
        private readonly IDatabase _redis;
        private readonly ILogger<TokenBucketRateLimiter> _logger;
        private readonly LuaScript _luaScript;
        private readonly int _bucketCapacity;
        private readonly double _refillRatePerSecond;

        public TokenBucketRateLimiter(
            IConnectionMultiplexer connectionMultiplexer,
            ILogger<TokenBucketRateLimiter> logger,
            int bucketCapacity,
            double refillRatePerSecond)
        {
            _redis = connectionMultiplexer.GetDatabase();
            _logger = logger;
            _bucketCapacity = bucketCapacity;
            _refillRatePerSecond = refillRatePerSecond;

            // Lua script:
            // KEYS[1] = token key
            // KEYS[2] = timestamp key
            // ARGV[1] = current timestamp (ms)
            // ARGV[2] = bucket capacity
            // ARGV[3] = refill rate per second
            _luaScript = LuaScript.Prepare(@"
                local tokens_key = KEYS[1]
                local timestamp_key = KEYS[2]
                local now = tonumber(ARGV[1])
                local capacity = tonumber(ARGV[2])
                local refill_rate = tonumber(ARGV[3])
 
                local last_tokens = tonumber(redis.call('GET', tokens_key) or capacity)
                local last_refill = tonumber(redis.call('GET', timestamp_key) or now)
 
                local elapsed = now - last_refill
                local refill = math.floor(elapsed * refill_rate / 1000)
                local tokens = math.min(capacity, last_tokens + refill)
 
                if tokens <= 0 then
                    return 0
                else
                    tokens = tokens - 1
                    redis.call('SET', tokens_key, tokens)
                    redis.call('SET', timestamp_key, now)
                    redis.call('PEXPIRE', tokens_key, 60000)
                    redis.call('PEXPIRE', timestamp_key, 60000)
                    return 1
                end
            ");
        }

        public async Task<bool> IsAllowedAsync(string key)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var redisKeys = new RedisKey[]
            {
                new RedisKey($"token_bucket:{key}:tokens"),
                new RedisKey($"token_bucket:{key}:timestamp")
            };
            var redisArgs = new RedisValue[]
            {
                now,
                _bucketCapacity,
                _refillRatePerSecond
            };

            try
            {
                var result = (int)await _redis.ScriptEvaluateAsync(_luaScript.OriginalScript, redisKeys, redisArgs);
                return result == 1;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TokenBucketRateLimiter failed for key {Key}", key);
                return true; // fail-open strategy
            }
        }
    }
}
