using System.Net;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Logging;

namespace SosuBot.ScoresObserver;

public class RateLimitingHandler(ILogger<RateLimitingHandler> logger, int executionsPerMinute, int queueLimit = 1000)
    : DelegatingHandler
{
    private readonly RateLimiter _rateLimiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
    {
        TokenLimit = executionsPerMinute,
        ReplenishmentPeriod = TimeSpan.FromMinutes(1),
        TokensPerPeriod = executionsPerMinute,
        AutoReplenishment = true,
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        QueueLimit = queueLimit // allow some queueing; tune as needed
    });

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Acquire a permit — this waits but is cancellable.
        using var lease = await _rateLimiter.AcquireAsync(1, cancellationToken);
        if (!lease.IsAcquired)
        {
            logger.LogWarning(
                $"Acquired: {lease.IsAcquired}, Rate limiter statistics: {JsonSerializer.Serialize(_rateLimiter.GetStatistics())}");
            var resp = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            resp.RequestMessage = request;
            return resp;
        }

        return await base.SendAsync(request, cancellationToken);
    }
}