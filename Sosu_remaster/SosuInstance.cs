using Sosu;
using Sosu.Services;
using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Sosu_remaster
{
    internal class SosuInstance
    {
        public readonly string token = "";
        public ITelegramBotClient bot;

        public HandleUpdateService HandleUpdateService;

        public SosuInstance(string token)
        {
            this.token = token;
            this.bot = new TelegramBotClient(token);
            this.HandleUpdateService = new HandleUpdateService(bot, null);
        }

        public async void Start()
        {
            await bot.DeleteWebhookAsync();

            Settings.LoadAllSettings();

            using var cts = new CancellationTokenSource();
            ReceiverOptions ro = new Telegram.Bot.Polling.ReceiverOptions() { AllowedUpdates = Array.Empty<UpdateType>() };
            bot.StartReceiving(UpdateHandler, ErrorHandler, ro, cts.Token);

            Variables.bot = await bot.GetMeAsync();

            var saveDataTimer = new System.Timers.Timer(10 * 60 * 1000);
            saveDataTimer.Elapsed += (s, e) =>
            {
                try
                {
                    TextDatabase.SaveData();
                    Console.WriteLine("saved");
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            };
            saveDataTimer.Start();
        }

        private Task UpdateHandler(ITelegramBotClient bot, Update update, CancellationToken ct)
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("en-US");
            return HandleUpdateService.EchoAsync(update);
        }
        private Task ErrorHandler(ITelegramBotClient bot, Exception exception, CancellationToken ct)
        {
            Console.WriteLine(exception);
            return Task.CompletedTask;
        }
    }
}
