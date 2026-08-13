namespace SosuBot.TelegramHandlers.Commands;

public sealed class OsuLastWithCoverCommand : OsuLastCommand
{
    public new static readonly string[] Commands = ["/last", "/l"];
    public new static readonly string Description = "[osuname] [count] последние игры с обложкой карты";

    public OsuLastWithCoverCommand() : base(false, true)
    {
    }
}
