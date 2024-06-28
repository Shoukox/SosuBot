namespace Sosu.Web.Services.HandleUpdateType
{
    public interface IHandler
    {
        public Task HandleAsync();
        public Task HandleErrorAsync(Exception exception);
    }
}
