namespace SosuBot.Services.Handlers.Abstract
{
    public interface ICommandBase<TUpdateType> where TUpdateType : class
    {
        public void SetContext(ICommandContext<TUpdateType> context);
        public abstract Task ExecuteAsync();
    }
}
