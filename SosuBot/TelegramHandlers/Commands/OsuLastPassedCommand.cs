namespace SosuBot.TelegramHandlers.Commands;

public sealed class OsuLastPassedCommand : OsuLastCommand
{
    public new static readonly string[] Commands = ["/lastpassed", "/lastp", "/lp"];
    public new static readonly string Description = "[osuname] [count] последние пройденные игры";

    public OsuLastPassedCommand() : base(onlyPassed: true, sendCover: true)
    {
    }
}
