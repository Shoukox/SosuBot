using SosuBot.TelegramHandlers.Abstract;
using Telegram.Bot.Types;

namespace SosuBot.TelegramHandlers.Commands;

public sealed class DummyCommand : CommandBase<Message>
{
    public static readonly string Description = "служебная команда-заглушка";

    public override Task ExecuteAsync()
    {
        return Task.CompletedTask;
    }
}
