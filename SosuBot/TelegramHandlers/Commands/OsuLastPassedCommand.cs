namespace SosuBot.TelegramHandlers.Commands;

public sealed class OsuLastPassedCommand : OsuLastCommand
{
    public new static readonly string[] Commands = ["/lastpassed", "/lastp", "/lp"];

    public OsuLastPassedCommand() : base(true)
    {
    }
}