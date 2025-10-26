using System.Net;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Extensions.Http;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace SosuBot;

public static class PollyPolicies
{
    private static Random _jitterer = new();

    private static IAsyncPolicy<HttpResponseMessage> GetTransientRetryPolicy(ILogger logger)
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                Backoff.DecorrelatedJitterBackoffV2(TimeSpan.FromSeconds(1), 3),
                (_, delay, attempt, _) =>
                {
                    logger.LogWarning(
                        "Transient error (attempt {Attempt}). Waiting {Delay} before retry.", attempt,
                        delay);
                });
    }

    private static IAsyncPolicy<HttpResponseMessage> GetRetryAfterPolicy(ILogger logger)
    {
        return Policy
            .HandleResult<HttpResponseMessage>(r =>
                r.StatusCode == HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(
                3,
                (_, response, _) =>
                {
                    var ra = response.Result.Headers.RetryAfter;

                    if (ra == null) return TimeSpan.FromSeconds(5);

                    if (ra.Delta.HasValue) return ra.Delta.Value;
                    if (ra.Date.HasValue)
                    {
                        var delta = ra.Date.Value - DateTimeOffset.UtcNow;
                        return delta > TimeSpan.Zero ? delta : TimeSpan.FromSeconds(1);
                    }

                    return TimeSpan.FromSeconds(10);
                },
                async (_, timespan, retryCount, _) =>
                {
                    logger.LogWarning("Received 429. Retrying after {Delay} (retry {Retry}).", timespan, retryCount);
                    await Task.CompletedTask;
                });
    }

    public static IAsyncPolicy<HttpResponseMessage> GetCombinedPolicy(ILogger logger)
    {
        var transientRetryPolicy = GetTransientRetryPolicy(logger);
        var retryAfterPolicy = GetRetryAfterPolicy(logger);

        return Policy.WrapAsync(transientRetryPolicy, retryAfterPolicy);
    }
}