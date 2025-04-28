using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OsuApi.Core.V2;
using SosuBot.Database;
using SosuBot.Logging;
using SosuBot.Services;
using SosuBot.Services.Data;
using SosuBot.Services.Handlers;
using Telegram.Bot;

namespace SosuBot;

internal class Program
{
    static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        // Configuration
        string fileName = "appsettings.json";
        if (!File.Exists(fileName)) throw new FileNotFoundException($"{fileName} was not found!", fileName);
        builder.Configuration.AddJsonFile(fileName, false);

        // Logging
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Logging.AddConsoleFormatter<CustomConsoleFormatter, CustomConsoleFormatterOptions>();

        // Services
        builder.Services.Configure<BotConfiguration>(builder.Configuration.GetSection(nameof(BotConfiguration)));
        builder.Services.Configure<OsuApiV2Configuration>(builder.Configuration.GetSection(nameof(OsuApiV2Configuration)));

        builder.Services.AddHttpClient(nameof(TelegramBotClient))
                        .RemoveAllLoggers()
                        .AddTypedClient<ITelegramBotClient>((httpClient, sp) =>
                        {
                            var options = sp.GetRequiredService<IOptions<BotConfiguration>>();
                            return new TelegramBotClient(options.Value.Token, httpClient);
                        });
        var osuApiV2Configuration = builder.Services.BuildServiceProvider().GetRequiredService<IOptions<OsuApiV2Configuration>>().Value;
        builder.Services.AddSingleton<ApiV2>(new ApiV2(osuApiV2Configuration.ClientId, osuApiV2Configuration.ClientSecret));
        builder.Services.AddSingleton<UpdateQueueService>();
        builder.Services.AddScoped<UpdateHandler>();
        builder.Services.AddHostedService<PollingBackgroundService>();
        builder.Services.AddHostedService<UpdateHandlerBackgroundService>();

        // Database
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string" + "'DefaultConnection' not found.");
        builder.Services.AddDbContextPool<BotContext>(options => options.UseSqlite(connectionString));

        var app = builder.Build();
        app.Run();
    }
}
