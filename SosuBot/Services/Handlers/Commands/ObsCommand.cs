using Microsoft.Extensions.Logging;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.Localization;
using SosuBot.Services.Handlers.Abstract;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace SosuBot.Services.Handlers.Commands
{
    //todo
    public class ObsCommand : CommandBase<Message>
    {
        public static string[] Commands = ["/obs"];

        public override async Task ExecuteAsync()
        {
            OsuUser? osuUserInDatabase = await Context.Database.OsuUsers.FindAsync(Context.Update.From!.Id);

            if (osuUserInDatabase is null || !osuUserInDatabase.IsAdmin) return;

            Message waitMessage = await Context.Update.ReplyAsync(Context.BotClient, "Подожди...");
            string[] parameters = Context.Update.Text!.GetCommandParameters()!;
        }
    }
}
