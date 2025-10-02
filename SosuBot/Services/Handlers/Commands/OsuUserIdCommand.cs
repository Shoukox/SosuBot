namespace SosuBot.Services.Handlers.Commands;

public sealed class OsuUserIdCommand : OsuUserCommand
{
    public new static readonly string[] Commands = ["/userid", "/ui"];

    public OsuUserIdCommand() : base(true)
    {
        
    }
}