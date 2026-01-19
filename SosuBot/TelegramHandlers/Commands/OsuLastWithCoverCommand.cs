namespace SosuBot.TelegramHandlers.Commands;

public sealed class OsuLastWithCoverCommand : OsuLastCommand
{
    public new static readonly string[] Commands = ["/ll"];

    public OsuLastWithCoverCommand() : base(false, true)
    {
    }
}