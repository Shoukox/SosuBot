namespace SosuBot.TelegramHandlers.Commands;

public sealed class OsuLastRichCommand : OsuLastCommand
{
    public new static readonly string[] Commands = ["/lw"];
    public new static readonly string Description = "[osuname] [count] последние игры в виде rich-сообщения";

    public OsuLastRichCommand() : base(richMessage: true)
    {
    }
}
