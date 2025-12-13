using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SosuBot;

public static class ServiceCollectionExtensions
{
    public static IHttpClientBuilder AddCustomHttpClient(this IServiceCollection services, string name,
        int executionsPerMinute)
    {
        return services.AddHttpClient(name)
            .ConfigureHttpClient(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(40);
                client.DefaultRequestHeaders.ConnectionClose = true;
            })
            .AddHttpMessageHandler(sp =>
                new RateLimitingHandler(sp.GetRequiredService<ILogger<RateLimitingHandler>>(), executionsPerMinute));
    }
}