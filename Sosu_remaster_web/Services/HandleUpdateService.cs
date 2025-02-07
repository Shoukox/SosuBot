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

        public Task EchoAsync(Update update)
        {
            Func<ITelegramBotClient,Update,Task> handler = update.Type switch
            {
                UpdateType.Message => (new ProcessMessage()).OnReceived,
                UpdateType.EditedMessage => (new ProcessMessage()).OnReceived,
                UpdateType.CallbackQuery => (new ProcessCallbackQuery()).OnReceived,
                UpdateType.Unknown => Nothing,
                UpdateType.InlineQuery => Nothing,
                UpdateType.ChosenInlineResult => Nothing,
                UpdateType.ChannelPost => Nothing,
                UpdateType.EditedChannelPost => Nothing,
                UpdateType.ShippingQuery => Nothing,
                UpdateType.PreCheckoutQuery => Nothing,
                UpdateType.Poll => Nothing,
                UpdateType.PollAnswer => Nothing,
                UpdateType.MyChatMember => Nothing,
                UpdateType.ChatMember => Nothing,
                UpdateType.ChatJoinRequest => Nothing,
                _ => Nothing,
            };

            try
            {
                _ = Task.Run(() => handler(_botClient, update));
            }
            catch (Exception exception)
            {
                _ = HandleErrorAsync(exception);
            }

            return Task.CompletedTask;
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

        public Task Nothing(ITelegramBotClient bot, Update update)
        {
            return Task.CompletedTask;
        }
    }
}
