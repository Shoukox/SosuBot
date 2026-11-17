using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace SosuBot.Services.Synchronization
{
    /// <summary>
    /// Token bucket shared by all bot replicas through Redis. Each limiter keeps its own buckets: limiters with
    /// different names never share state, even for the same key.
    /// </summary>
    public class TokenBucketRateLimiter
    {
        // KEYS[1] = bucket hash (fields: tokens, ts)
        // ARGV[1] = bucket capacity
        // ARGV[2] = refill rate, tokens per millisecond
        // ARGV[3] = bucket lifetime when idle, ms
        // Tokens are kept fractional, so slow buckets (a few per hour) refill even between frequent requests.
        // Redis' own clock is used, so replicas with skewed clocks agree.
        private const string Script = @"
            local key = KEYS[1]
            local capacity = tonumber(ARGV[1])
            local refill_per_ms = tonumber(ARGV[2])
            local ttl_ms = tonumber(ARGV[3])

            local time = redis.call('TIME')
            local now = tonumber(time[1]) * 1000 + math.floor(tonumber(time[2]) / 1000)

            local state = redis.call('HMGET', key, 'tokens', 'ts')
            local tokens = tonumber(state[1])
            local last = tonumber(state[2])
            if tokens == nil or last == nil then
                tokens = capacity
                last = now
            end

            tokens = math.min(capacity, tokens + math.max(0, now - last) * refill_per_ms)

            local allowed = 0
            if tokens >= 1 then
                tokens = tokens - 1
                allowed = 1
            end

            redis.call('HSET', key, 'tokens', tostring(tokens), 'ts', tostring(now))
            redis.call('PEXPIRE', key, ttl_ms)
            return allowed
        ";

        private readonly IDatabase _redis;
        private readonly ILogger<TokenBucketRateLimiter> _logger;
        private readonly string _name;
        private readonly int _bucketCapacity;
        private readonly double _refillRatePerMillisecond;
        private readonly long _idleLifetimeMilliseconds;
        private readonly SemaphoreSlim _waitLock = new(1, 1);

        /// <param name="name">Distinguishes this limiter's buckets from other limiters' in Redis.</param>
        public TokenBucketRateLimiter(
            IConnectionMultiplexer connectionMultiplexer,
            ILogger<TokenBucketRateLimiter> logger,
            string name,
            int bucketCapacity,
            double refillRatePerSecond)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bucketCapacity);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(refillRatePerSecond);

            _redis = connectionMultiplexer.GetDatabase();
            _logger = logger;
            _name = name;
            _bucketCapacity = bucketCapacity;
            _refillRatePerMillisecond = refillRatePerSecond / 1000;
            // After this long without requests the bucket is full again, so dropping it changes nothing.
            _idleLifetimeMilliseconds = Math.Max(1_000, (long)Math.Ceiling(bucketCapacity / _refillRatePerMillisecond));
        }

        public async Task<bool> IsAllowedAsync(string key)
        {
            var redisKeys = new RedisKey[] { GetBucketKey(key) };
            var redisArgs = new RedisValue[]
            {
                _bucketCapacity,
                _refillRatePerMillisecond,
                _idleLifetimeMilliseconds
            };

            try
            {
                var result = (int)await _redis.ScriptEvaluateAsync(Script, redisKeys, redisArgs);
                return result == 1;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Token bucket rate limiter failed");
                return true; // fail-open strategy
            }
        }

        internal string GetBucketKey(string key) => $"token_bucket:{_name}:{key}";

        public async Task WaitAsync(string key, CancellationToken cancellationToken = default)
        {
            await _waitLock.WaitAsync(cancellationToken);
            try
            {
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (await IsAllowedAsync(key))
                        return;

                    await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
                }
            }
            finally
            {
                _waitLock.Release();
            }
        }
    }
}
