using SosuBot.Services.Handlers.Abstract;
using Telegram.Bot.Types;

namespace SosuBot.Services.Handlers.Callbacks;

public class DummyCommand : CommandBase<CallbackQuery>
{
    public override Task ExecuteAsync()
    {
        return Task.CompletedTask;
    }
}