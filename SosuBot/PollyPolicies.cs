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
                onRetry: (outcome, delay, attempt, context) =>
                {
                    if (outcome.Exception != null)
                    {
                        // vollständige Exception (inkl. Stack) loggen
                        logger.LogWarning(outcome.Exception,
                            "Transient exception (attempt {Attempt}). Waiting {Delay} before retry. Exception: {Message}",
                            attempt, delay, outcome.Exception.Message);
                    }
                    else if (outcome.Result != null)
                    {
                        logger.LogWarning(
                            "Transient HTTP response (attempt {Attempt}). StatusCode: {StatusCode}. Waiting {Delay} before retry. ReasonPhrase: {Reason}",
                            attempt, (int)outcome.Result.StatusCode, delay, outcome.Result.ReasonPhrase);
                    }
                    else
                    {
                        logger.LogWarning("Transient error (attempt {Attempt}). Waiting {Delay} before retry.", attempt, delay);
                    }
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

                    logger.LogWarning("Retry-After header is present but has no Delta or Date. Using default delay.");
                    return TimeSpan.FromSeconds(10);
                },
                (_, timespan, retryCount, _) =>
                {
                    logger.LogWarning("Received 429. Retrying after {Delay} (retry {Retry}).", timespan, retryCount);
                    return Task.CompletedTask;
                });
    }

    public static IAsyncPolicy<HttpResponseMessage> GetCombinedPolicy(ILogger logger)
    {
        var transientRetryPolicy = GetTransientRetryPolicy(logger);
        var retryAfterPolicy = GetRetryAfterPolicy(logger);

        return Policy.WrapAsync(transientRetryPolicy, retryAfterPolicy);
    }
}