using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OsuApi.V2;
using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Extensions.Http;
using SosuBot.Database;
using SosuBot.Logging;
using SosuBot.Services;
using SosuBot.Services.BackgroundServices;
using SosuBot.Services.Handlers;
using Telegram.Bot;

namespace SosuBot;

internal class Program
{
    private static ILogger<Program>? _logger;

    private static void Main(string[] args)
    {
        Run(args);
    }

    private static void Run(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        // Configuration
        var configurationFileName = "appsettings.json";
        if (!File.Exists(configurationFileName))
            throw new FileNotFoundException($"{configurationFileName} was not found!", configurationFileName);
        builder.Configuration.AddJsonFile(configurationFileName, false);

        var openaiConfigurationFileName = "openai-settings.json";
        if (!File.Exists(openaiConfigurationFileName))
            throw new FileNotFoundException($"{openaiConfigurationFileName} was not found!", configurationFileName);
        builder.Configuration.AddJsonFile(openaiConfigurationFileName, false);

        // Logging
        var loggingFileName = "logs/{Date}.log";
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Logging.AddFile(loggingFileName, LogLevel.Error);
        builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));
        builder.Logging.AddConsoleFormatter<CustomConsoleFormatter, CustomConsoleFormatterOptions>();

        // Instantiate a logger for this class
        _logger = builder.Services.BuildServiceProvider().GetRequiredService<ILogger<Program>>();

        // Handling fatal errors 
        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
        {
            HandleFatalError(eventArgs.ExceptionObject as Exception);
        };
        TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
        {
            HandleFatalError(eventArgs.Exception);
            eventArgs.SetObserved();
        };

        // Services
        builder.Services.Configure<BotConfiguration>(builder.Configuration.GetSection(nameof(BotConfiguration)));
        builder.Services.Configure<OsuApiV2Configuration>(
            builder.Configuration.GetSection(nameof(OsuApiV2Configuration)));
        builder.Services.Configure<OpenAiConfiguration>(builder.Configuration.GetSection(nameof(OpenAiConfiguration)));
        builder.Services.AddHttpClient(nameof(ITelegramBotClient))
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
            .AddPolicyHandler(GetRetryPolicy());


        var osuApiV2Configuration = builder.Services.BuildServiceProvider()
            .GetRequiredService<IOptions<OsuApiV2Configuration>>().Value;
        builder.Services.AddSingleton<ApiV2>(provider =>
        {
            var config = osuApiV2Configuration;
            var httpClient = provider.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(ITelegramBotClient));
            httpClient.DefaultRequestHeaders.ConnectionClose = true;
            return new ApiV2(config.ClientId, config.ClientSecret, httpClient);
        });
        builder.Services.AddSingleton<UpdateQueueService>();
        builder.Services.AddSingleton<RabbitMqService>();
        builder.Services.AddSingleton<OpenAiService>();
        builder.Services.AddScoped<UpdateHandler>();
        builder.Services.AddHostedService<PollingBackgroundService>();
        builder.Services.AddHostedService<UpdateHandlerBackgroundService>();
        builder.Services.AddHostedService<ScoresObserverBackgroundService>();

        // Database
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ??
                               throw new InvalidOperationException("Connection string" +
                                                                   "'DefaultConnection' not found.");
        builder.Services.AddDbContextPool<BotContext>(options => options.UseSqlite(connectionString));

        var app = builder.Build();
        app.Run();
    }

    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        var transientRetryPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                Backoff.DecorrelatedJitterBackoffV2(TimeSpan.FromSeconds(1), 3),
                (delay, attempt, outcome, _) =>
                {
                    _logger!.LogWarning(
                        "Transient error (attempt {Attempt}). Waiting {Delay} before retry. Status: {Status}", attempt,
                        delay, outcome);
                });

        var retryAfterPolicy = Policy
            .HandleResult<HttpResponseMessage>(r =>
                r.StatusCode == HttpStatusCode.TooManyRequests && r.Headers.RetryAfter != null)
            .WaitAndRetryAsync(
                3,
                (_, response, _) =>
                {
                    var ra = response.Result.Headers.RetryAfter!;
                    if (ra.Delta.HasValue) return ra.Delta.Value;
                    if (ra.Date.HasValue)
                    {
                        var delta = ra.Date.Value - DateTimeOffset.UtcNow;
                        return delta > TimeSpan.Zero ? delta : TimeSpan.FromSeconds(1);
                    }

                    return TimeSpan.FromSeconds(1);
                },
                async (_, timespan, retryCount, _) =>
                {
                    _logger!.LogWarning("Received 429. Retrying after {Delay} (retry {Retry}).", timespan, retryCount);
                    await Task.CompletedTask;
                });

        return Policy.WrapAsync(retryAfterPolicy, transientRetryPolicy);
    }

    private static void HandleFatalError(Exception? ex)
    {
        _logger!.LogCritical(ex, "Fatal error");
    }
}