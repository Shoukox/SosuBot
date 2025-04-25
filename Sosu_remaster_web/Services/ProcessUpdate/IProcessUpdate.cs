using Telegram.Bot;
using Telegram.Bot.Types;

namespace Sosu.Services.ProcessUpdate
{
    public interface IProcessUpdate
    {
        public Task OnReceived(ITelegramBotClient bot, Update update); //async
    }
}
