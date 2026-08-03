using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using OsuApi.BanchoV2;
using Polly;
using SosuBot.Database;
using SosuBot.Database.Models;
using SosuBot.ScoresObserver;
using SosuBot.ScoresObserver.Extensions;
using SosuBot.ScoresObserver.Logging;
using SosuBot.ScoresObserver.Monitoring;
using SosuBot.ScoresObserver.Services;
using System.Globalization;

if (args.Contains("--healthcheck", StringComparer.Ordinal))
{
    int port = int.TryParse(Environment.GetEnvironmentVariable("Monitoring__Port"), out int configuredPort)
        ? configuredPort
        : 9092;
    using var healthClient = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
    using HttpResponseMessage response = await healthClient.GetAsync($"http://127.0.0.1:{port}/metrics");
    response.EnsureSuccessStatusCode();
    return;
}

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddFile("logs/{Date}.log", LogLevel.Warning);
builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));
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
        options.ConfigureScope(scope => scope.SetTag("service", "sosubot-scores-observer"));
    });
}

builder.Services.AddOptions<OsuApiV2Configuration>()
    .Bind(builder.Configuration.GetRequiredSection(nameof(OsuApiV2Configuration)))
    .Validate(configuration => configuration.ClientId > 0, "osu! API ClientId must be greater than zero")
    .Validate(configuration => !string.IsNullOrWhiteSpace(configuration.ClientSecret),
        "osu! API ClientSecret is required")
    .ValidateOnStart();
builder.Services.AddOptions<ScoresObserverConfiguration>()
    .Bind(builder.Configuration.GetSection(nameof(ScoresObserverConfiguration)))
    .Validate(configuration => configuration.ScoresLimit is > 0 and <= 100,
        "ScoresLimit must be between 1 and 100")
    .Validate(configuration => configuration.LeaderboardPlayers is > 0 and <= 50,
        "LeaderboardPlayers must be between 1 and 50")
    .Validate(configuration => configuration.UserPollDelay > TimeSpan.Zero,
        "UserPollDelay must be positive")
    .ValidateOnStart();
builder.Services.Configure<MonitoringConfiguration>(builder.Configuration.GetSection("Monitoring"));

builder.Services.AddSingleton<IAsyncPolicy<HttpResponseMessage>>(provider =>
    PollyPolicies.GetCombinedPolicy(provider.GetRequiredService<ILogger<Program>>()));
builder.Services.AddCustomHttpClient(nameof(BanchoApiV2), 300)
    .AddPolicyHandler((provider, _) => provider.GetRequiredService<IAsyncPolicy<HttpResponseMessage>>());
builder.Services.AddSingleton<BanchoApiV2>(provider =>
{
    OsuApiV2Configuration configuration = provider.GetRequiredService<IOptions<OsuApiV2Configuration>>().Value;
    HttpClient httpClient = provider.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(BanchoApiV2));
    ILogger<BanchoApiV2> logger = provider.GetRequiredService<ILogger<BanchoApiV2>>();
    return new BanchoApiV2(configuration.ClientId, configuration.ClientSecret, httpClient);
});
builder.Services.AddSingleton<UserStatisticsCacheDatabase>();
builder.Services.AddSingleton<ObserverMetrics>();
builder.Services.AddHostedService<MetricsServerHostedService>();
builder.Services.AddHostedService<ScoresObserverBackgroundService>();
builder.Services.AddHostedService<DailyStatisticsReportOutboxBackgroundService>();

string configuredConnectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("Connection string 'Postgres' is not configured.");

var connectionStringBuilder = new NpgsqlConnectionStringBuilder(configuredConnectionString);
string connectionString = connectionStringBuilder.ConnectionString;

builder.Services.AddDbContextPool<BotContext>(options =>
    options.UseLazyLoadingProxies()
        .UseNpgsql(connectionString, npgsql => npgsql.MapEnum<Playmode>())
        .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning)));
builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));

IHost app = builder.Build();
app.Services.GetRequiredService<ILogger<Program>>().LogInformation(
    "Using PostgreSQL at {Host}:{Port}/{Database} as {Username}",
    connectionStringBuilder.Host,
    connectionStringBuilder.Port,
    connectionStringBuilder.Database,
    connectionStringBuilder.Username);
await app.RunAsync();
