using System.Globalization;
using System.Timers;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace Sosu.Services
{

    public class ConfigureWebhookService : IHostedService
    {
        private readonly ILogger<ConfigureWebhookService> _logger;
        private readonly IServiceProvider _services;
        private readonly BotConfiguration _botConfig;

        public ConfigureWebhookService(ILogger<ConfigureWebhookService> logger,
                                IServiceProvider serviceProvider,
                                IConfiguration configuration)
        {
            _logger = logger;
            _services = serviceProvider;
            _botConfig = configuration.GetSection("BotConfiguration").Get<BotConfiguration>();

            Settings.LoadAllSettings();
        }
      
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("en-US");

            using var scope = _services.CreateScope();
            var botClient = scope.ServiceProvider.GetRequiredService<ITelegramBotClient>();
            Variables.bot = await botClient.GetMeAsync();
            var webhookAddress = @$"{_botConfig.HostAddress}/bot/{_botConfig.BotToken}";
            _logger.LogDebug("Setting webhook: " + webhookAddress);
            await botClient.SetWebhookAsync(
                url: webhookAddress,
                allowedUpdates: Array.Empty<UpdateType>(),
                cancellationToken: cancellationToken);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            //nothing
        }
    }
}
