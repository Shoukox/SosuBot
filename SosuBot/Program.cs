using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SosuBot.Database;
using SosuBot.Logging;
using SosuBot.Services;
using Telegram.Bot;

namespace SosuBot;

internal class Program
{
    static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        string token = builder.Configuration.GetSection("TelegramBotToken").Value!;
        var botConfiguration = new BotConfiguration() { Token = token };

        // Logging
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Logging.AddConsoleFormatter<CustomConsoleFormatter, CustomConsoleFormatterOptions>();

        builder.Services.Configure<BotConfiguration>(builder.Configuration.GetSection("BotConfiguration"));
        builder.Services.AddHttpClient("telegram_bot_client")
                        .RemoveAllLoggers()
                        .AddTypedClient<ITelegramBotClient>((httpClient, sp) =>
                        {
                            BotConfiguration? botConfiguration = sp.GetService<IOptions<BotConfiguration>>()?.Value;
                            ArgumentNullException.ThrowIfNull(botConfiguration);
                            TelegramBotClientOptions options = new(botConfiguration.Token);
                            return new TelegramBotClient(options, httpClient);
                        });

        builder.Services.AddScoped<UpdateHandler>();
        builder.Services.AddScoped<ReceiverService>();
        builder.Services.AddHostedService<PollingService>();
        // Database
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string" + "'DefaultConnection' not found.");
        builder.Services.AddDbContextPool<BotContext>(options => options.UseSqlite(connectionString));

        // Services
        builder.Services.AddSingleton(botConfiguration); // bot config
                                                         //builder.Services.AddSingleton<IDiscordServerStateCollection, DiscordServerStateCollection>();
                                                         //builder.Services.AddSingleton<IDiscordServerStateCollection, DiscordServerStateCollection>();

        var app = builder.Build();
        app.Run();
    }
}
