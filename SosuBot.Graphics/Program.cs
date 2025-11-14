
namespace SosuBot.Graphics
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            var app = builder.Build();
            app.UseHttpsRedirection();
            app.MapGet("/", async () => OsuCardHandler.HandleHttpRequest);
            app.Run();
        }
    }
}
