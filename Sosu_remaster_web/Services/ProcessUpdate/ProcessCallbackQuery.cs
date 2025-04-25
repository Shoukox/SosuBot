using Sosu.Services.ProcessUpdate.CallbacksCommands;
using Sosu.Services.ProcessUpdate.MessageCommands;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Sosu.Services.ProcessUpdate
{
    public class ProcessCallbackQuery : IProcessUpdate
    {
        public async Task OnReceived(ITelegramBotClient bot, Update update)
        {
            CallbackQuery callbackQuery = update.CallbackQuery;

            string[] splittedMessageText = callbackQuery.Data.Split(" ");
            string command = splittedMessageText[1];

            ICommand handler = command switch
            {
                "user" => new OsuUserCallbackCommand(),
                "userbest" => new OsuUserBestCallbackCommand(),
                "songpreview" => new OsuSongPreviewCallbackCommand(),
                _ => new DummyCommand()
            };

            await handler.action(bot, update);
        }
    }
}
