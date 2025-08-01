﻿using System.Net;
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
using SosuBot.Services.BackgroundServices;
using SosuBot.Services.Data;
using SosuBot.Services.Handlers;
using Telegram.Bot;

namespace SosuBot;

internal class Program
{
    private static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        // Configuration
        var fileName = "appsettings.json";
        if (!File.Exists(fileName)) throw new FileNotFoundException($"{fileName} was not found!", fileName);
        builder.Configuration.AddJsonFile(fileName, false);

        // Logging
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Logging.AddConsoleFormatter<CustomConsoleFormatter, CustomConsoleFormatterOptions>();

        // Services
        builder.Services.Configure<BotConfiguration>(builder.Configuration.GetSection(nameof(BotConfiguration)));
        builder.Services.Configure<OsuApiV2Configuration>(
            builder.Configuration.GetSection(nameof(OsuApiV2Configuration)));
        builder.Services.AddHttpClient(nameof(TelegramBotClient))
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
            var httpClient = provider.GetRequiredService<IHttpClientFactory>().CreateClient();
            return new ApiV2(config.ClientId, config.ClientSecret, httpClient);
        });
        builder.Services.AddSingleton<UpdateQueueService>();
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
        var transientRetryPolicy =
            HttpPolicyExtensions
                .HandleTransientHttpError()
                .WaitAndRetryAsync(Backoff.DecorrelatedJitterBackoffV2(TimeSpan.FromSeconds(1), 5));

        var retryAfterPolicy = Policy
            .HandleResult<HttpResponseMessage>(r =>
                r.StatusCode == HttpStatusCode.TooManyRequests && r.Headers.RetryAfter != null)
            .WaitAndRetryAsync(3,
                (_, result, _) => result.Result.Headers.RetryAfter!.Delta!.Value,
                (_, _, _, _) => Task.CompletedTask);

        return Policy.WrapAsync(transientRetryPolicy, retryAfterPolicy);
    }
}