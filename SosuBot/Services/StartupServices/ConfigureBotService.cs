using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SosuBot.Database;
using SosuBot.Services.BackgroundServices;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace SosuBot.Services.StartupServices;

public class ConfigureBotService(IServiceProvider serviceProvider) : IHostedService
{
    private ITelegramBotClient _botClient = serviceProvider.GetRequiredService<ITelegramBotClient>(); 
    private ILogger<ConfigureBotService> _logger = serviceProvider.GetRequiredService<ILogger<ConfigureBotService>>();

    private static IEnumerable<BotCommand> botCommands = [
        new("/help", "Список всех команд"),
    ];
    
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _botClient.SetMyCommands(botCommands, cancellationToken: CancellationToken.None);
        _logger.LogInformation("Successfully set bot commands");
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}