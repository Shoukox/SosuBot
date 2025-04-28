using Telegram.Bot;

namespace SosuBot.Services.Handlers
{
    public interface IRequestHandler<TUpdate> where TUpdate : class
    {
        public Task OnReceived(ITelegramBotClient bot, TUpdate update); //async
    }
}
