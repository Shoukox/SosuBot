using Sosu.Localization;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Sosu.Services.ProcessUpdate.MessageCommands
{
    public class DummyCommand : ICommand
    {
        //public string pattern
        //{
        //    get => "/start";
        //}
        public Func<ITelegramBotClient, Update, Task> action => new Func<ITelegramBotClient, Update, Task>((bot, update) =>
            {
                return Task.CompletedTask;
            });
    }
}
