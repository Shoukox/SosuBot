using System.Security.AccessControl;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OsuApi.V2;
using SosuBot.Database;
using SosuBot.Helpers.Types;
using SosuBot.Logging;
using SosuBot.Services;
using SosuBot.Services.BackgroundServices;
using SosuBot.Services.Handlers;
using StackExchange.Redis;
using Telegram.Bot;

namespace SosuBot;

internal class Program
{
    private static ILogger<Program>? _logger;
    private static string _redisContainerName = "redis-service";

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

        // OpenAI configuration
        var openaiConfigurationFileName = "openai-settings.json";
        if (!File.Exists(openaiConfigurationFileName))
            throw new FileNotFoundException($"{openaiConfigurationFileName} was not found!", configurationFileName);
        builder.Configuration.AddJsonFile(openaiConfigurationFileName, false);

        // Logging
        var loggingFileName = "logs/{Date}.log";
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Logging.AddFile(loggingFileName, LogLevel.Warning);
        builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));
        builder.Logging.AddConsoleFormatter<CustomConsoleFormatter, CustomConsoleFormatterOptions>();
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
        builder.Services
            .AddCustomHttpClient(nameof(ITelegramBotClient), short.MaxValue)
            .AddTypedClient<ITelegramBotClient>((httpClient, sp) =>
            {
                var options = sp.GetRequiredService<IOptions<BotConfiguration>>();
                return new TelegramBotClient(options.Value.Token, httpClient);
            })
            .AddPolicyHandler(PollyPolicies.GetCombinedPolicy(_logger));

        builder.Services
            .AddCustomHttpClient("CustomHttpClient", 1000)
            .AddPolicyHandler(PollyPolicies.GetCombinedPolicy(_logger));
        ;

        var osuApiV2Configuration = builder.Services.BuildServiceProvider()
            .GetRequiredService<IOptions<OsuApiV2Configuration>>().Value;
        builder.Services.AddSingleton<ApiV2>(provider =>
        {
            var config = osuApiV2Configuration;
            var httpClient = provider.GetRequiredService<IHttpClientFactory>().CreateClient("CustomHttpClient");
            var logger = provider.GetRequiredService<ILogger<ApiV2>>();
            return new ApiV2(config.ClientId, config.ClientSecret, httpClient, logger);
        });

        builder.Services.AddSingleton<UpdateQueueService>();
        builder.Services.AddSingleton<RabbitMqService>();
        builder.Services.AddSingleton<OpenAiService>();
        builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(_redisContainerName));
        builder.Services.AddScoped<UpdateHandler>();
        builder.Services.AddHostedService<PollingBackgroundService>();
        builder.Services.AddHostedService<UpdateHandlerBackgroundService>();
        builder.Services.AddHostedService<ScoresObserverBackgroundService>();

        // Database
        var pwFile = Environment.GetEnvironmentVariable("DB_PASSWORD_FILE") ??
                     Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", ".secrets",
                         "db_password");
        var dbPassword = File.ReadAllText(pwFile).Trim();
        ;
        var host = Environment.GetEnvironmentVariable("DB_HOST") ?? "localhost";
        var port = Environment.GetEnvironmentVariable("DB_PORT") ?? "5432";
        var db = Environment.GetEnvironmentVariable("DB_NAME") ?? "sosubot";
        var user = Environment.GetEnvironmentVariable("DB_USER") ?? "sosubot";
        var connectionString = $"Host={host};Port={port};Database={db};Username={user};Password={dbPassword}";

        bool usePostgres = Environment.GetEnvironmentVariable("USE_POSTGRES") == "YES";
        if (!usePostgres)
        {
            connectionString = $"Data Source=bot.db";
        }

        _logger.LogInformation($"Using the following connection string: {connectionString}");

        if (usePostgres)
        {
            builder.Services.AddDbContextPool<BotContext>(options =>
                options.UseNpgsql(connectionString, (m) => m.MapEnum<Playmode>())
                    .ConfigureWarnings(m => m.Ignore(RelationalEventId.PendingModelChangesWarning)));
        }
        else
        {
            builder.Services.AddDbContextPool<BotContext>(options =>
                options.UseSqlite(connectionString)
                    .ConfigureWarnings(m => m.Ignore(RelationalEventId.PendingModelChangesWarning)));
        }

        bool shouldMigrate = Environment.GetEnvironmentVariable("DB_MIGRATE") == "YES";
        if (shouldMigrate)
        {
            var database = builder.Services.BuildServiceProvider().GetRequiredService<BotContext>();
            database.Database.Migrate();
        }

        var app = builder.Build();
        app.Run();
    }

    private static void HandleFatalError(Exception? ex)
    {
        _logger!.LogCritical(ex, "Fatal error");
    }
}