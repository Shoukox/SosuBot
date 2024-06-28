using Telegram.Bot;
using Telegram.Bot.Types;

namespace Sosu.Web.Services.HandleUpdateType
{
    public class HandleMessage : IHandler
    {
        private readonly Message _message;
        private readonly ITelegramBotClient _botClient;

        public HandleMessage(Message message, ITelegramBotClient botClient)
        {
            _message = message;
            _botClient = botClient;
        }

        public Task HandleAsync()
        {
            return _botClient.SendTextMessageAsync(_message.Chat.Id, $"Got message: {_message.Text}");
        }

        public Task HandleErrorAsync(Exception exception)
        {
            throw new NotImplementedException();
        }
    }
}
