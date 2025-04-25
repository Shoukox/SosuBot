using Telegram.Bot;
using Telegram.Bot.Types;

namespace Sosu.Services.ProcessUpdate
{
    public interface ICommand
    {
        //public string pattern { get; }
        public Func<ITelegramBotClient, Update, Task> action { get; }
    }
}
