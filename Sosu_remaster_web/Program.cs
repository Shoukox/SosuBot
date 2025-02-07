using Serilog;
using Serilog.Events;
using Sosu.Services;
using Telegram.Bot;

namespace Sosu
{
    public class Program
    {

        public static async Task Main(string[] args)
        {
            await MainWeb(args);
        }

        public static Task MainWeb(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            Variables.botConfiguration = builder.Configuration.GetSection(nameof(BotConfiguration)).Get<BotConfiguration>()!;

            builder.Services.AddControllers();
            builder.Services.AddHostedService<ConfigureWebhookService>();
            builder.Services.AddHttpClient("tg_webhook").AddTypedClient<ITelegramBotClient>(httpclient => new TelegramBotClient(Variables.botConfiguration.BotToken, httpclient));
            builder.Services.AddScoped<HandleUpdateService>();
            builder.Services.AddControllers()
                    .AddNewtonsoftJson();

            var app = builder.Build();
            

            app.UseEndpoints(m =>
            {
                var token = Variables.botConfiguration.BotToken;
                m.MapControllerRoute(
                        name: "tg_webhook",
                        pattern: $"bot/{token}",
                        new { controller = "Webhook", action = "Post" }
                    );
                m.MapControllers();
            });

            return app.RunAsync();
        }
    }
}