using Microsoft.Extensions.Options;
using Sosu.Main.Services;
using Telegram.Bot;

namespace Sosu.Web.Services
{
    public class ConfigureWebhook : IHostedService
    {
        private readonly ILogger<ConfigureWebhook> _logger;
        private readonly ITelegramBotClient _botClient;
        private readonly IOptions<BotConfiguration> _configuration;

        public ConfigureWebhook(ITelegramBotClient botClient, IOptions<BotConfiguration> configuration, ILogger<ConfigureWebhook> logger)
        {
            _botClient = botClient;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            string webhookUrl = $"{_configuration.Value.HostAdress}/bot/{_configuration.Value.Token}";
            _logger.LogInformation("Setting Webhook to a botClient with host:{0}", webhookUrl);
            await _botClient.SetWebhookAsync(webhookUrl);

            _logger.LogInformation("Setted Webhook to a botClient with host:{0}", webhookUrl);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {

        }
    }
}
