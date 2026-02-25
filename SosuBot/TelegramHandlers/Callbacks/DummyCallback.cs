using SosuBot.Extensions;
using SosuBot.TelegramHandlers.Abstract;
using Telegram.Bot.Types;

namespace SosuBot.TelegramHandlers.Callbacks;

public class DummyCallback : CommandBase<CallbackQuery>
{
    public override async Task ExecuteAsync()
    {
        await Context.Update.AnswerAsync(Context.BotClient);
    }
}
