namespace SosuBot.Services.Handlers.Abstract;

public abstract class CommandBase<TUpdateType> : ICommandBase<TUpdateType> where TUpdateType : class
{
    /// <summary>
    ///     Use <see cref="SetContext(TContext)" /> before calling <see cref="ExecuteAsync()" />!
    /// </summary>
    protected ICommandContext<TUpdateType> Context { get; set; } = null!;

    public void SetContext(ICommandContext<TUpdateType> context)
    {
        Context = context;
    }

    public virtual Task ExecuteAsync()
    {
        return BeforeExecuteAsync();
    }

    public virtual Task BeforeExecuteAsync()
    {
        return Task.CompletedTask;
    }
}