using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OsuApi.V2;
using SosuBot.Database;
using SosuBot.Database.Models;
using SosuBot.ScoresObserver;
using SosuBot.ScoresObserver.Extensions;
using SosuBot.ScoresObserver.Logging;
using SosuBot.ScoresObserver.Services;

var builder = Host.CreateApplicationBuilder(args);

// Logging
var loggingFileName = "logs/{Date}.log";
builder.Logging.AddFile(loggingFileName, LogLevel.Warning);
builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));
builder.Logging.AddConsoleFormatter<CustomConsoleFormatter, CustomConsoleFormatterOptions>();
ILogger logger = builder.Services.BuildServiceProvider().GetRequiredService<ILogger<Program>>();

// Policy
var pollyPolicies = PollyPolicies.GetCombinedPolicy(logger);

// Services
builder.Services.Configure<OsuApiV2Configuration>(builder.Configuration.GetSection(nameof(OsuApiV2Configuration)));
builder.Services.AddCustomHttpClient(nameof(ApiV2), 300).AddPolicyHandler(pollyPolicies);
builder.Services.AddSingleton<ApiV2>(provider =>
{
    var config = builder.Configuration.Get<OsuApiV2Configuration>()!;
    var httpClient = provider.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(ApiV2));
    var logger = provider.GetRequiredService<ILogger<ApiV2>>();
    return new ApiV2(config.ClientId, config.ClientSecret, httpClient, logger);
});
builder.Services.AddHostedService<ScoresObserverBackgroundService>();

// Database
var pwFile = Environment.GetEnvironmentVariable("DB_PASSWORD_FILE")!;
var dbPassword = File.ReadAllText(pwFile).Trim();
var connectionString = string.Format(builder.Configuration.GetConnectionString("Postgres")!, dbPassword);
logger.LogInformation($"Using the following connection string: {connectionString}");

builder.Services.AddDbContextPool<BotContext>(options =>
    options.UseLazyLoadingProxies()
        .UseNpgsql(connectionString, (m) => m.MapEnum<Playmode>())
        .ConfigureWarnings(m => m.Ignore(RelationalEventId.PendingModelChangesWarning)));

var app = builder.Build();
app.Run();