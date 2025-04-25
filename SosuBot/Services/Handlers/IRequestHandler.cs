using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace SosuBot.Services.Handlers
{
    public interface IRequestHandler<TUpdate> where TUpdate : class
    {
        public Task OnReceived(ITelegramBotClient bot, TUpdate update); //async
    }
}
