using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;

namespace SosuBot;

public static class ServiceCollectionExtensions
{
    public static IHttpClientBuilder AddCustomHttpClient(this IServiceCollection services, string name)
    {
        return services.AddHttpClient(name)
            .ConfigureHttpClient(client =>
            {
                client.Timeout = Timeout.InfiniteTimeSpan;
                client.DefaultRequestHeaders.ConnectionClose = true;
            });
    }
}