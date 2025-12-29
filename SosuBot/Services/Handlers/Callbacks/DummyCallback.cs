using SosuBot.Extensions;
using SosuBot.Services.Handlers.Abstract;
using Telegram.Bot.Types;

namespace SosuBot.Services.Handlers.Callbacks;

public class DummyCallback : CommandBase<CallbackQuery>
{
    public override async Task ExecuteAsync()
    {
        await Context.Update.AnswerAsync(Context.BotClient);
    }
}