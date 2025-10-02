using SosuBot.Services.Handlers.Abstract;
using Telegram.Bot.Types;

namespace SosuBot.Services.Handlers.Commands;

public sealed class DummyCommand : CommandBase<Message>
{
    public override Task ExecuteAsync()
    {
        return Task.CompletedTask;
    }
}