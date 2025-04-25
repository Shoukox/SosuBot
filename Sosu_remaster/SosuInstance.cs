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
        public readonly string Token;
        public readonly ITelegramBotClient Bot;

        public readonly HandleUpdateService HandleUpdateService;

        public SosuInstance(string token)
        {
            this.Token = token;
            this.Bot = new TelegramBotClient(token);
            this.HandleUpdateService = new HandleUpdateService(Bot, null);
        }

        public async Task Start()
        {
            await Bot.DeleteWebhookAsync();

            Settings.LoadAllSettings();

            using var cts = new CancellationTokenSource();
            ReceiverOptions ro = new ReceiverOptions() { AllowedUpdates = Array.Empty<UpdateType>(), ThrowPendingUpdates = true};
            Bot.StartReceiving(UpdateHandler, ErrorHandler, ro, cts.Token);

            Variables.bot = await Bot.GetMeAsync();
            TextDatabase.SaveTimer();
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
