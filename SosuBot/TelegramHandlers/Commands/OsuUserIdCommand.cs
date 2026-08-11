namespace SosuBot.TelegramHandlers.Commands;

public sealed class OsuUserIdCommand : OsuUserCommand
{
    public new static readonly string[] Commands = ["/userid", "/ui"];
    public new static readonly string Description = "[user_id] найти игрока по точному ID";

    public OsuUserIdCommand() : base(true)
    {
    }
}
