using Telegram.Bot;
using Telegram.Bot.Types;

namespace Sosu.Web.Services.HandleUpdateType
{
    public class HandleUnknown : IHandler
    {
        private readonly ITelegramBotClient _botClient;

        public HandleUnknown(ITelegramBotClient botClient)
        {
            _botClient = botClient;
        }
        public Task HandleAsync()
        {
            throw new NotImplementedException();
        }

        public Task HandleErrorAsync(Exception exception)
        {
            throw new NotImplementedException();
        }
    }
}
