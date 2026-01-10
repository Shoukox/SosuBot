using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OsuApi.V2;
using SosuBot.Database;
using SosuBot.Database.Models;
using SosuBot.Helpers;
using SosuBot.Helpers.OutputText;
using SosuBot.Logging;
using SosuBot.Services;
using SosuBot.Services.BackgroundServices;
using SosuBot.Services.Handlers;
using SosuBot.Services.StartupServices;
using SosuBot.Services.Synchronization;
using StackExchange.Redis;
using TagLib.Matroska;
using Telegram.Bot;

namespace SosuBot;

internal class Program
{
    private static void Main(string[] args)
    {
        Run(args);
    }

    private static void Run(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        // Configuration
        var configurationFileName = "appsettings.json";
        if (!System.IO.File.Exists(configurationFileName))
            throw new FileNotFoundException($"{configurationFileName} was not found!", configurationFileName);

        // OpenAI configuration
        var openaiConfigurationFileName = "openai-settings.json";
        if (!System.IO.File.Exists(openaiConfigurationFileName))
            throw new FileNotFoundException($"{openaiConfigurationFileName} was not found!", configurationFileName);
        builder.Configuration.AddJsonFile(openaiConfigurationFileName, false);

        // Logging
        var loggingFileName = "logs/{Date}.log";
        builder.Logging.AddFile(loggingFileName, LogLevel.Warning);
        builder.Logging.AddConsoleFormatter<CustomConsoleFormatter, CustomConsoleFormatterOptions>();

        // Policy
        var pollyPolicies = PollyPolicies.GetCombinedPolicy();

        // Services
        builder.Services.Configure<BotConfiguration>(builder.Configuration.GetSection(nameof(BotConfiguration)));
        builder.Services.Configure<OsuApiV2Configuration>(builder.Configuration.GetSection(nameof(OsuApiV2Configuration)));
        builder.Services.Configure<OpenAiConfiguration>(builder.Configuration.GetSection(nameof(OpenAiConfiguration)));
        builder.Services
            .AddCustomHttpClient(nameof(ITelegramBotClient), 32_767)
            .AddTypedClient<ITelegramBotClient>((httpClient, sp) =>
            {
                var options = sp.GetRequiredService<IOptions<BotConfiguration>>();
                return new TelegramBotClient(options.Value.Token, httpClient);
            })
            .AddPolicyHandler(pollyPolicies);
        builder.Services
            .AddCustomHttpClient("CustomHttpClient", 300)
            .AddPolicyHandler(pollyPolicies);
        ;

        var osuApiV2Configuration = builder.Services.BuildServiceProvider()
            .GetRequiredService<IOptions<OsuApiV2Configuration>>().Value;
        builder.Services.AddSingleton(provider =>
        {
            var config = osuApiV2Configuration;
            var httpClient = provider.GetRequiredService<IHttpClientFactory>().CreateClient("CustomHttpClient");
            var logger = provider.GetRequiredService<ILogger<ApiV2>>();
            return new ApiV2(config.ClientId, config.ClientSecret, httpClient, logger);
        });

        builder.Services.AddSingleton<CachingHelper>();
        builder.Services.AddSingleton<ScoreHelper>();
        builder.Services.AddSingleton<UpdateQueueService>();
        builder.Services.AddSingleton(serviceProvider =>
        {
            var logger = serviceProvider.GetRequiredService<ILogger<ReplayRenderService>>();
            return new ReplayRenderService(new("https://sosubot.shoukko.de"), logger); //https://sosubot.shoukko.de http://localhost:5000
        });
        builder.Services.AddSingleton<OpenAiService>();
        builder.Services.AddSingleton<BeatmapsService>();

        // Redis
        var redisHost = Environment.GetEnvironmentVariable("REDIS_HOST") ?? "localhost";
        int port = 6379;
        var redisConfigurationOptions = new ConfigurationOptions()
        {
            EndPoints =
                {
                    { redisHost, port }
                },
            KeepAlive = 10,
            AbortOnConnectFail = false,
            ConnectTimeout = 2000
        };
        builder.Services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConfigurationOptions.ToString();
            options.InstanceName = $"SosuBot{Environment.TickCount}:";
        });
        builder.Services.AddHybridCache(options =>
        {
            options.DefaultEntryOptions = new HybridCacheEntryOptions()
            {
                Flags = HybridCacheEntryFlags.DisableLocalCache,
                Expiration = TimeSpan.FromMinutes(60)
            };
        });
        builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConfigurationOptions));

        // Redis RateLimiter
        builder.Services.AddSingleton(provide =>
        {
            var redis = provide.GetRequiredService<IConnectionMultiplexer>();
            var logger = provide.GetRequiredService<ILogger<TokenBucketRateLimiter>>();
            return new RateLimiterFactory(redis, logger);
        });

        builder.Services.AddScoped<UpdateHandler>();
        builder.Services.AddHostedService<ConfigureBotService>();
        builder.Services.AddHostedService<PollingBackgroundService>();
        builder.Services.AddHostedService<UpdateHandlerBackgroundService>();
        builder.Services.AddHostedService<ScoresObserverBackgroundService>();

        // Database
        var connectionString = builder.Configuration.GetConnectionString("Postgres")!;
        Log($"Using the following connection string: {connectionString}");
        builder.Services.AddDbContextPool<BotContext>(options =>
            options.UseLazyLoadingProxies()
                .UseNpgsql(connectionString, (m) => m.MapEnum<Playmode>())
                .ConfigureWarnings(m => m.Ignore(RelationalEventId.PendingModelChangesWarning)));

        var app = builder.Build();
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BotContext>();
            db.Database.Migrate();
        }
        app.Run();
    }

    private static void Log(string message)
    {
        Console.WriteLine($"\x1b[32m[{DateTime.Now:yyyy-MM-dd HH:mm:ss.ffff}][Program] \x1b[37m{message}\x1b[0m");
    }
}