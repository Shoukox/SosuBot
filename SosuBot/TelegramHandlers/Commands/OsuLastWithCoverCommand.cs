namespace SosuBot.TelegramHandlers.Commands;

public sealed class OsuLastWithCoverCommand : OsuLastCommand
{
    public new static readonly string[] Commands = ["/l"];

    public OsuLastWithCoverCommand() : base(false, true)
    {
    }
}
