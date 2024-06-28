using Sosu.Services.ProcessUpdate;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Sosu.Services
{
    public class HandleUpdateService
    {
        private readonly ITelegramBotClient _botClient;
        private readonly ILogger<HandleUpdateService> _logger;

        public HandleUpdateService(ITelegramBotClient botClient, ILogger<HandleUpdateService> logger)
        {
            _botClient = botClient;
            _logger = logger;
        }

        public async Task EchoAsync(Update update)
        {
            var handler = update.Type switch
            {
                UpdateType.Message => (new ProcessMessage()).OnReceived(_botClient, update),
                UpdateType.EditedMessage => (new ProcessMessage()).OnReceived(_botClient, update),
                UpdateType.CallbackQuery => (new ProcessCallbackQuery()).OnReceived(_botClient, update),
                UpdateType.Unknown => Nothing(),
                UpdateType.InlineQuery => Nothing(),
                UpdateType.ChosenInlineResult => Nothing(),
                UpdateType.ChannelPost => Nothing(),
                UpdateType.EditedChannelPost => Nothing(),
                UpdateType.ShippingQuery => Nothing(),
                UpdateType.PreCheckoutQuery => Nothing(),
                UpdateType.Poll => Nothing(),
                UpdateType.PollAnswer => Nothing(),
                UpdateType.MyChatMember => Nothing(),
                UpdateType.ChatMember => Nothing(),
                UpdateType.ChatJoinRequest => Nothing(),
                _ => Nothing(),
            };

            try
            {
                await Task.Run(async() => handler);
            }
            catch (Exception exception)
            {
                await HandleErrorAsync(exception);
            }
        }



        public Task HandleErrorAsync(Exception exception)
        {
            var ErrorMessage = exception switch
            {
                ApiRequestException apiRequestException => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => exception.ToString()
            };

            if (_logger != null)
                _logger.LogError(ErrorMessage);
            else
                Console.WriteLine(ErrorMessage);
            return Task.CompletedTask;
        }

        public async Task Nothing()
        {
            //Diese Methode macht nichts
        }
    }
}
