using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;

namespace SosuBot;

public static class ServiceCollectionExtensions
{
    public static IHttpClientBuilder AddCustomHttpClient(this IServiceCollection services, string name, ILogger logger, int executionsPerOneSecond, int executionsPerOneMinute)
    {
        return services.AddHttpClient(name)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                // Replace connections every 2 minutes to avoid stale/closed sockets
                PooledConnectionLifetime = TimeSpan.FromMinutes(2),

                // Drop idle connections after 1 minute
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(1),

                // How many concurrent connections to the Telegram API
                MaxConnectionsPerServer = 100,

                // Short connect timeout (fail fast if remote unreachable)
                ConnectTimeout = TimeSpan.FromSeconds(10)
            })
            .ConfigureHttpClient(client =>
            {
                // Make request timeout slightly larger than your long polling timeout.
                client.Timeout = TimeSpan.FromSeconds(62);
            })
            .AddTypedClient<ITelegramBotClient>((httpClient, sp) =>
            {
                var options = sp.GetRequiredService<IOptions<BotConfiguration>>();
                return new TelegramBotClient(options.Value.Token, httpClient);
            })
            .AddPolicyHandler(PollyPolicies.GetCombinedPolicy(logger, executionsPerOneSecond, executionsPerOneMinute));
    }
}