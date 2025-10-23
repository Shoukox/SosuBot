using System.Threading.RateLimiting;
using Microsoft.Extensions.Logging;

namespace SosuBot;

public class RateLimitingHandler(ILogger<RateLimitingHandler> logger, int executionsPerMinute) : DelegatingHandler
{
    private readonly RateLimiter _rateLimiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
    {
        TokenLimit = executionsPerMinute,
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        QueueLimit = executionsPerMinute, // allow some queueing; tune as needed
        ReplenishmentPeriod = TimeSpan.FromMinutes(1),
        TokensPerPeriod = executionsPerMinute,
        AutoReplenishment = true
    });

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Acquire a permit — this waits but is cancellable.
        using var lease = await _rateLimiter.AcquireAsync(1, cancellationToken);
        logger.LogInformation($"Acquired: {lease.IsAcquired}, Available permits: {_rateLimiter.GetStatistics()?.CurrentAvailablePermits}");
        if (!lease.IsAcquired)
        {
            // we were cancelled or couldn't acquire — return 429 or throw
            // Throwing allows caller to see cancellation; returning 429 is another option.
            var resp = new HttpResponseMessage(System.Net.HttpStatusCode.TooManyRequests);
            resp.RequestMessage = request;
            return resp;
        }

        // Proceed to actual HTTP call
        return await base.SendAsync(request, cancellationToken);
    }
}