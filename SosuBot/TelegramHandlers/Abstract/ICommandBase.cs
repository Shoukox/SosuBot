namespace SosuBot.TelegramHandlers.Abstract;

public interface ICommandBase<TUpdateType> where TUpdateType : class
{
    public void SetContext(ICommandContext<TUpdateType> context);
    public Task ExecuteAsync();
}
