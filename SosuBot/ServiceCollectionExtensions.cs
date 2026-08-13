using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;

using SosuBot.Monitoring;

namespace SosuBot;

public static class ServiceCollectionExtensions
{
    public static IHttpClientBuilder AddCustomHttpClient(
        this IServiceCollection services,
        string name,
        int executionsPerMinute,
        TimeSpan? timeout = null,
        int? executionsPerSecond = null,
        IAsyncPolicy<HttpResponseMessage>? retryPolicy = null)
    {
        IHttpClientBuilder builder = services.AddHttpClient(name);

        // Keep retries outside the rate limiter so every retry is rate-limited.
        if (retryPolicy is not null)
            builder.AddPolicyHandler(retryPolicy);

        return builder
            .ConfigureHttpClient(client =>
            {
                client.Timeout = timeout ?? TimeSpan.FromSeconds(120);
                client.DefaultRequestHeaders.ConnectionClose = true;
            })
            .AddHttpMessageHandler(sp => new OutboundHttpMetricsHandler(
                sp.GetRequiredService<BotMetrics>(),
                name))
            .AddHttpMessageHandler(sp => new RateLimitingHandler(
                sp.GetRequiredService<ILogger<RateLimitingHandler>>(),
                executionsPerMinute,
                executionsPerSecond));
    }
}
