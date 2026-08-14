using Polly;
using Polly.Contrib.WaitAndRetry;
using System.Net;
using System.Net.Http.Headers;

namespace SosuBot;

public static class PollyPolicies
{
    private static readonly TimeSpan[] TelegramRetryDelays =
    [
        TimeSpan.Zero,
        TimeSpan.FromMilliseconds(500),
        TimeSpan.FromSeconds(1.5)
    ];

    private static IAsyncPolicy<HttpResponseMessage> GetTransientRetryPolicy()
    {
        return CreateTransientRetryPolicy(
            Backoff.DecorrelatedJitterBackoffV2(TimeSpan.FromSeconds(1), 3));
    }

    private static IAsyncPolicy<HttpResponseMessage> CreateTransientRetryPolicy(
        IEnumerable<TimeSpan> retryDelays,
        bool handleTimeoutCancellation = false)
    {
        PolicyBuilder<HttpResponseMessage> policy = Policy<HttpResponseMessage>
            .Handle<HttpRequestException>(IsTransientHttpRequestException)
            .OrResult(response =>
                response.StatusCode == HttpStatusCode.RequestTimeout ||
                (int)response.StatusCode >= 500)
            .Or<TaskCanceledException>(exception =>
                handleTimeoutCancellation &&
                (exception.InnerException is TimeoutException ||
                 !exception.CancellationToken.IsCancellationRequested))
            .Or<TimeoutException>(_ => handleTimeoutCancellation);

        return policy.WaitAndRetryAsync(
                retryDelays,
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

    private static bool IsTransientHttpRequestException(HttpRequestException exception)
    {
        if (TryGetStatusCode(exception, out HttpStatusCode statusCode))
        {
            return statusCode == HttpStatusCode.RequestTimeout ||
                   statusCode == HttpStatusCode.TooManyRequests ||
                   (int)statusCode >= 500;
        }

        // Exceptions without an HTTP status code are normally transport
        // failures (DNS, connection reset, timeout, etc.).
        return true;
    }

    private static bool TryGetStatusCode(HttpRequestException exception, out HttpStatusCode statusCode)
    {
        if (exception.StatusCode is { } exceptionStatusCode)
        {
            statusCode = exceptionStatusCode;
            return true;
        }

        // OsuApi.Core 0.1.0 puts the status code only in the exception text.
        const string marker = "status code ";
        int markerIndex = exception.Message.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        int statusCodeStart = markerIndex + marker.Length;
        if (markerIndex >= 0 &&
            statusCodeStart + 3 <= exception.Message.Length &&
            int.TryParse(exception.Message.AsSpan(statusCodeStart, 3), out int numericStatusCode) &&
            Enum.IsDefined(typeof(HttpStatusCode), numericStatusCode))
        {
            statusCode = (HttpStatusCode)numericStatusCode;
            return true;
        }

        statusCode = default;
        return false;
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
                    RetryConditionHeaderValue? ra = response.Result.Headers.RetryAfter;

                    if (ra == null) return TimeSpan.FromSeconds(5);

                    if (ra.Delta.HasValue) return ra.Delta.Value;
                    if (ra.Date.HasValue)
                    {
                        TimeSpan delta = ra.Date.Value - DateTimeOffset.UtcNow;
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
        IAsyncPolicy<HttpResponseMessage> transientRetryPolicy = GetTransientRetryPolicy();
        IAsyncPolicy<HttpResponseMessage> retryAfterPolicy = GetRetryAfterPolicy();

        return Policy.WrapAsync(transientRetryPolicy, retryAfterPolicy);
    }

    public static IAsyncPolicy<HttpResponseMessage> GetTelegramPolicy()
    {
        IAsyncPolicy<HttpResponseMessage> transientRetryPolicy = CreateTransientRetryPolicy(
            TelegramRetryDelays,
            handleTimeoutCancellation: true);
        IAsyncPolicy<HttpResponseMessage> retryAfterPolicy = GetRetryAfterPolicy();

        return Policy.WrapAsync(transientRetryPolicy, retryAfterPolicy);
    }

    private static void Log(string message)
    {
        Console.WriteLine($"\x1b[32m[{DateTime.Now:yyyy-MM-dd HH:mm:ss.ffff}][{nameof(PollyPolicies)}] \x1b[37m{message}\x1b[0m");
    }
}
