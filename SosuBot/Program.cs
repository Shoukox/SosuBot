using System.Net;
using System.Threading.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OsuApi;
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
        builder.Services.AddCustomHttpClient(nameof(ITelegramBotClient), _logger, Int16.MaxValue, Int16.MaxValue * 60);
        builder.Services.AddCustomHttpClient(nameof(ApiV2), _logger, Int16.MaxValue, 1200);

        var osuApiV2Configuration = builder.Services.BuildServiceProvider()
            .GetRequiredService<IOptions<OsuApiV2Configuration>>().Value;
        builder.Services.AddSingleton<ApiV2>(provider =>
        {
            var config = osuApiV2Configuration;
            var httpClient = provider.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(ApiV2));
            var logger = provider.GetRequiredService<ILogger<ApiV2>>();
            httpClient.DefaultRequestHeaders.ConnectionClose = true;
            return new ApiV2(config.ClientId, config.ClientSecret, httpClient, logger);
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

    private static void HandleFatalError(Exception? ex)
    {
        _logger!.LogCritical(ex, "Fatal error");
    }
}