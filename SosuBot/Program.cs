using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using OsuApi.BanchoV2;
using Polly;
using SosuBot.Configuration;
using SosuBot.Calculators.Official;
using SosuBot.Database;
using SosuBot.Database.Models;
using SosuBot.Graphics;
using SosuBot.Helpers;
using SosuBot.Logging;
using SosuBot.Monitoring;
using SosuBot.PerformanceCalculator;
using SosuBot.Services;
using SosuBot.Services.BackgroundServices;
using SosuBot.Services.StartupServices;
using SosuBot.Services.Synchronization;
using SosuBot.TelegramHandlers;
using StackExchange.Redis;
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
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

        // Configuration
        var configurationFileName = "appsettings.json";
        bool requestedMigrateOnly = builder.Configuration.GetValue("Database:MigrateOnly", false);
        if (!requestedMigrateOnly && !File.Exists(configurationFileName))
            throw new FileNotFoundException($"{configurationFileName} was not found!", configurationFileName);

        // Logging
        var loggingFileName = "logs/{Date}.log";
        builder.Logging.AddFile(loggingFileName, LogLevel.Warning);
        builder.Logging.AddConsoleFormatter<CustomConsoleFormatter, CustomConsoleFormatterOptions>();

        string? sentryDsn = builder.Configuration["Sentry:Dsn"];
        if (!string.IsNullOrWhiteSpace(sentryDsn))
        {
            builder.Logging.AddSentry(options =>
            {
                options.Dsn = sentryDsn;
                options.Environment = builder.Configuration["Sentry:Environment"] ?? builder.Environment.EnvironmentName;
                options.Release = builder.Configuration["Sentry:Release"];
                options.MinimumBreadcrumbLevel = LogLevel.Error;
                options.MinimumEventLevel = LogLevel.Error;
                options.SendDefaultPii = false;
                options.IsEnvironmentUser = false;
                options.AttachStacktrace = true;
                options.EnableLogs = builder.Configuration.GetValue("Sentry:EnableLogs", false);
                options.TracesSampleRate = builder.Configuration.GetValue("Sentry:TracesSampleRate", 0.1);
                options.CaptureFailedRequests = false;
                options.TracePropagationTargets.Clear();
                options.MaxBreadcrumbs = 50;
                options.SetBeforeSend(sentryEvent =>
                    sentryEvent.Exception is OperationCanceledException ? null : sentryEvent);
                options.ConfigureScope(scope =>
                {
                    scope.SetTag("service", "sosubot");
                    scope.SetTag("runtime", ".NET 10 Generic Host");
                });
            });
        }

        // Policy
        IAsyncPolicy<HttpResponseMessage> pollyPolicies = PollyPolicies.GetCombinedPolicy();

        // Services
        IConfigurationSection botConfig = builder.Configuration.GetSection(nameof(BotConfiguration));
        IConfigurationSection renderConfig = builder.Configuration.GetSection(nameof(RenderConfiguration));
        builder.Services.Configure<BotConfiguration>(botConfig);
        builder.Services.Configure<BeatmapsConfiguration>(builder.Configuration.GetSection(nameof(BeatmapsConfiguration)));
        builder.Services.Configure<OsuApiV2Configuration>(builder.Configuration.GetSection(nameof(OsuApiV2Configuration)));
        builder.Services.Configure<OpenAiConfiguration>(builder.Configuration.GetSection(nameof(OpenAiConfiguration)));
        builder.Services.Configure<RenderConfiguration>(renderConfig);
        builder.Services.Configure<MonitoringConfiguration>(builder.Configuration.GetSection("Monitoring"));
        builder.Services.AddSingleton<BotMetrics>();
        builder.Services.AddSingleton<IPerformanceCalculator, PPCalculator>();
        builder.Services.AddSingleton<CommandUsageRecorder>();
        builder.Services.AddHostedService<MetricsServerHostedService>();
        builder.Services.AddHostedService<MetricsSnapshotBackgroundService>();
        builder.Services.AddCustomHttpClient(nameof(ITelegramBotClient), 32_767)
                        .AddTypedClient<ITelegramBotClient>((httpClient, sp) =>
                        {
                            IOptions<BotConfiguration> options = sp.GetRequiredService<IOptions<BotConfiguration>>();
                            var telegramOptions = new TelegramBotClientOptions(options.Value.Token, baseUrl: botConfig[nameof(BotConfiguration.ApiServerUrl)]);
                            return new TelegramBotClient(telegramOptions, httpClient);
                        })
                        .AddPolicyHandler(pollyPolicies);
        builder.Services.AddCustomHttpClient("CustomHttpClient", 300)
                        .AddPolicyHandler(pollyPolicies);
        builder.Services.AddCustomHttpClient("OsuApiHttpClient", 300)
                        .AddHttpMessageHandler(() => new OsuApiAvailabilityHandler())
                        .AddPolicyHandler(pollyPolicies);
        builder.Services.AddCustomHttpClient(BeatmapsService.HttpClientName, 300)
                        .AddPolicyHandler(pollyPolicies);
        builder.Services.AddCustomHttpClient(nameof(ReplayRenderService), 32_767, TimeSpan.FromMinutes(10));

        builder.Services.AddSingleton(provider =>
        {
            OsuApiV2Configuration config = builder.Configuration.GetSection(nameof(OsuApiV2Configuration)).Get<OsuApiV2Configuration>()!;
            HttpClient httpClient = provider.GetRequiredService<IHttpClientFactory>().CreateClient("OsuApiHttpClient");
            ILogger<BanchoApiV2> logger = provider.GetRequiredService<ILogger<BanchoApiV2>>();
            return new BanchoApiV2(config.ClientId, config.ClientSecret, httpClient);
        });

        builder.Services.AddSingleton<CachingHelper>();
        builder.Services.AddSingleton<ScoreHelper>();
        builder.Services.AddSingleton<OfficialPerformanceHelper>();
        builder.Services.AddSingleton<PlayerSkillCalculator>();
        builder.Services.AddSingleton<ProfileCardGenerator>();
        builder.Services.AddSingleton<ScorePreviewGenerator>();
        builder.Services.AddSingleton<OsuCardService>();
        builder.Services.AddSingleton<VideoPreviewService>();
        builder.Services.AddSingleton<UpdateQueueService>();
        builder.Services.AddSingleton(serviceProvider =>
        {
            ILogger<ReplayRenderService> logger = serviceProvider.GetRequiredService<ILogger<ReplayRenderService>>();
            HttpClient httpClient = serviceProvider.GetRequiredService<IHttpClientFactory>()
                .CreateClient(nameof(ReplayRenderService));
            return new ReplayRenderService(
                new(renderConfig[nameof(RenderConfiguration.RenderUrl)]!),
                int.Parse(renderConfig[nameof(RenderConfiguration.ClientId)]!),
                renderConfig[nameof(RenderConfiguration.ClientSecret)]!,
                logger,
                httpClient);
        });
        builder.Services.AddSingleton<OpenAiService>();
        builder.Services.AddSingleton<BeatmapFileCache>();
        builder.Services.AddSingleton<BeatmapsService>();

        // Redis
        var redisHost = builder.Configuration["Redis:Host"]!;
        if(!int.TryParse(builder.Configuration["Redis:Port"], out int redisPort))
        {
            redisPort = 6379;
            Log("Failed to parse Redis port from configuration, defaulting to 6379");
        }

        var redisConfigurationOptions = new ConfigurationOptions()
        {
            EndPoints =
                {
                    { redisHost, redisPort }
                },
            KeepAlive = 10,
            AbortOnConnectFail = false,
            ConnectTimeout = 2000
        };
        builder.Services.AddStackExchangeRedisCache(options =>
        {
            options.ConfigurationOptions = redisConfigurationOptions;
            options.InstanceName = $"SosuBot{Environment.TickCount}:";
        });
        builder.Services.AddHybridCache(options =>
        {
            options.DefaultEntryOptions = new HybridCacheEntryOptions()
            {
                Flags = HybridCacheEntryFlags.DisableLocalCache
            };
        });

        // Redis RateLimiter
        builder.Services.AddSingleton(provide =>
        {
            var redis = ConnectionMultiplexer.Connect(redisConfigurationOptions);
            ILogger<TokenBucketRateLimiter> logger = provide.GetRequiredService<ILogger<TokenBucketRateLimiter>>();
            return new RateLimiterFactory(redis, logger);
        });

        builder.Services.AddScoped<UpdateHandler>();
        builder.Services.AddHostedService<ConfigureBotService>();
        builder.Services.AddHostedService<PollingBackgroundService>();
        builder.Services.AddHostedService<UpdateHandlerBackgroundService>();
        builder.Services.AddHostedService<TrackedScoreNotificationBackgroundService>();
        builder.Services.AddHostedService<DailyStatisticsReportDeliveryBackgroundService>();

        // Database
        string configuredConnectionString = builder.Configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Connection string 'Postgres' is not configured.");
        var connectionStringBuilder = new NpgsqlConnectionStringBuilder(configuredConnectionString);
        string? databasePasswordFile = builder.Configuration["Database:PasswordFile"];
        if (!string.IsNullOrWhiteSpace(databasePasswordFile))
        {
            if (!File.Exists(databasePasswordFile))
                throw new FileNotFoundException("PostgreSQL password file was not found.", databasePasswordFile);

            connectionStringBuilder.Password = File.ReadAllText(databasePasswordFile).Trim();
        }

        string connectionString = connectionStringBuilder.ConnectionString;
        Log($"Using PostgreSQL at {connectionStringBuilder.Host}:{connectionStringBuilder.Port}/" +
            $"{connectionStringBuilder.Database} as {connectionStringBuilder.Username}");
        builder.Services.AddDbContextPool<BotContext>(options =>
            options.UseLazyLoadingProxies()
                .UseNpgsql(connectionString, (m) => m.MapEnum<Playmode>())
                .ConfigureWarnings(m => m.Ignore(RelationalEventId.PendingModelChangesWarning)));

        IHost app = builder.Build();
        bool migrateOnly = requestedMigrateOnly;
        bool migrateOnStartup = builder.Configuration.GetValue("Database:MigrateOnStartup", true);
        if (migrateOnly || migrateOnStartup)
        {
            using IServiceScope scope = app.Services.CreateScope();
            BotContext database = scope.ServiceProvider.GetRequiredService<BotContext>();
            database.Database.Migrate();
        }

        if (migrateOnly)
        {
            Log("Database migrations completed; exiting migration-only mode");
            return;
        }

        app.Run();
    }

    private static void Log(string message)
    {
        Console.WriteLine($"\x1b[32m[{DateTime.Now:yyyy-MM-dd HH:mm:ss.ffff}][Program] \x1b[37m{message}\x1b[0m");
    }
}
