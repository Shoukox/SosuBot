using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace SosuBot.Services.Synchronization
{
    public sealed class RateLimiterFactory
    {
        private readonly IDictionary<RateLimitPolicy, TokenBucketRateLimiter> _limiters;

        public RateLimiterFactory(IConnectionMultiplexer redis, ILogger<TokenBucketRateLimiter> logger)
        {
            _limiters = new Dictionary<RateLimitPolicy, TokenBucketRateLimiter>
            {
                [RateLimitPolicy.Command] =
                    new TokenBucketRateLimiter(redis, logger, 5, 1.0), // 5 per 1 second

                [RateLimitPolicy.RenderCommand] =
                    new TokenBucketRateLimiter(redis, logger, 10, 10.0 / 3600.0) // 10 per 1 hour
            };
        }

        public TokenBucketRateLimiter Get(RateLimitPolicy policy)
            => _limiters[policy];

        public enum RateLimitPolicy
        {
            Command,
            RenderCommand
        }
    }
}
