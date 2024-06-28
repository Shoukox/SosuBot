using Microsoft.Extensions.Options;
using Sosu.Main.Services;
using Sosu.Web.Services;
using Telegram.Bot;

namespace Sosu.Main
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Configuration
            var section = builder.Configuration.GetSection(nameof(BotConfiguration));
            BotConfiguration botConfiguration = section.Get<BotConfiguration>();
            builder.Services.Configure<BotConfiguration>(section);

            // Add services to the container.
            builder.Services.AddControllers()
                .AddNewtonsoftJson();

            builder.Services.AddHostedService<ConfigureWebhook>();
            builder.Services.AddScoped<UpdateHandler>();

            builder.Services.AddHttpClient("tgbot").AddTypedClient<ITelegramBotClient>((httpClient, serviceProvider) =>
            {
                var token = botConfiguration.Token;
                return new TelegramBotClient(token, httpClient);
            });



            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseStaticFiles();

            app.UseRouting();
            app.MapControllerRoute(name: "default",
               pattern: $"bot/{botConfiguration.Token}");

            app.Run();
        }
    }
}