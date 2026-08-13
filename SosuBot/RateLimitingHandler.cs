using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net;
using System.Threading.RateLimiting;

namespace SosuBot;

public sealed class RateLimitingHandler : DelegatingHandler
{
    private readonly ILogger<RateLimitingHandler> _logger;
    private readonly RateLimiter _perMinuteRateLimiter;
    private readonly RateLimiter? _perSecondRateLimiter;

    public RateLimitingHandler(
        ILogger<RateLimitingHandler> logger,
        int executionsPerMinute,
        int? executionsPerSecond = null,
        int queueLimit = 1000)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(executionsPerMinute);
        if (executionsPerSecond is <= 0)
            throw new ArgumentOutOfRangeException(nameof(executionsPerSecond));
        ArgumentOutOfRangeException.ThrowIfNegative(queueLimit);

        _logger = logger;
        _perMinuteRateLimiter = CreateTokenBucketRateLimiter(
            executionsPerMinute,
            TimeSpan.FromMinutes(1),
            executionsPerMinute,
            queueLimit);
        _perSecondRateLimiter = executionsPerSecond is { } perSecond
            ? CreateTokenBucketRateLimiter(
                perSecond,
                TimeSpan.FromSeconds(1),
                perSecond,
                queueLimit)
            : null;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Acquire a permit — this waits but is cancellable. Every attempt must
        // pass through this handler, including Polly retries.
        using RateLimitLease perMinuteLease = await _perMinuteRateLimiter.AcquireAsync(1, cancellationToken);
        if (!perMinuteLease.IsAcquired)
            return CreateRateLimitedResponse(request, _perMinuteRateLimiter);

        if (_perSecondRateLimiter is null)
            return await base.SendAsync(request, cancellationToken);

        using RateLimitLease perSecondLease = await _perSecondRateLimiter.AcquireAsync(1, cancellationToken);
        if (!perSecondLease.IsAcquired)
            return CreateRateLimitedResponse(request, _perSecondRateLimiter);

        return await base.SendAsync(request, cancellationToken);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _perSecondRateLimiter?.Dispose();
            _perMinuteRateLimiter.Dispose();
        }

        base.Dispose(disposing);
    }

    private HttpResponseMessage CreateRateLimitedResponse(HttpRequestMessage request, RateLimiter rateLimiter)
    {
        _logger.LogWarning(
            "Rate limiter queue is full. Statistics: {Statistics}",
            JsonConvert.SerializeObject(rateLimiter.GetStatistics()));

        return new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            RequestMessage = request
        };
    }

    private static RateLimiter CreateTokenBucketRateLimiter(
        int tokenLimit,
        TimeSpan replenishmentPeriod,
        int tokensPerPeriod,
        int queueLimit)
    {
        return new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = tokenLimit,
            ReplenishmentPeriod = replenishmentPeriod,
            TokensPerPeriod = tokensPerPeriod,
            AutoReplenishment = true,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = queueLimit
        });
    }
}
