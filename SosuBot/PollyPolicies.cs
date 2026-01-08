using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Extensions.Http;
using System.Net;

namespace SosuBot;

public static class PollyPolicies
{
    private static IAsyncPolicy<HttpResponseMessage> GetTransientRetryPolicy()
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
                        Log($"Transient exception (attempt {attempt}). Waiting {delay} before retry. Exception: {outcome.Exception}");
                    }
                    else if (outcome.Result != null)
                    {
                        Log($"Transient HTTP response (attempt {attempt}). StatusCode: {(int)outcome.Result.StatusCode}. Waiting {delay} before retry. ReasonPhrase: {outcome.Result.ReasonPhrase}");
                    }
                    else
                    {
                        Log($"Transient error (attempt {attempt}). Waiting {delay} before retry.");
                    }
                });
    }


    private static IAsyncPolicy<HttpResponseMessage> GetRetryAfterPolicy()
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

                    Log("Retry-After header is present but has no Delta or Date. Using default delay.");
                    return TimeSpan.FromSeconds(10);
                },
                (_, timespan, retryCount, _) =>
                {
                    Log($"Received 429. Retrying after {timespan} (retry {retryCount}).");
                    return Task.CompletedTask;
                });
    }

    public static IAsyncPolicy<HttpResponseMessage> GetCombinedPolicy()
    {
        var transientRetryPolicy = GetTransientRetryPolicy();
        var retryAfterPolicy = GetRetryAfterPolicy();

        return Policy.WrapAsync(transientRetryPolicy, retryAfterPolicy);
    }

    private static void Log(string message)
    {
        Console.WriteLine($"\x1b[32m[{DateTime.Now:yyyy-MM-dd HH:mm:ss.ffff}][{nameof(PollyPolicies)}] \x1b[37m{message}\x1b[0m");
    }
}