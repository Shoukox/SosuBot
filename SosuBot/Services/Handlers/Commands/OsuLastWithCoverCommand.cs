namespace SosuBot.Services.Handlers.Commands;

public sealed class OsuLastWithCoverCommand : OsuLastCommand
{
    public new static readonly string[] Commands = ["/ll"];

    public OsuLastWithCoverCommand() : base(false, true)
    {
    }
}