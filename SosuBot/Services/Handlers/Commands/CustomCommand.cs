using Newtonsoft.Json;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.Helpers.OutputText;
using SosuBot.Services.Handlers.Abstract;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace SosuBot.Services.Handlers.Commands
{
    public class CustomCommand : CommandBase<Message>
    {
        public static string[] Commands = ["/c"];

        public override async Task ExecuteAsync()
        {
            OsuUser? osuUserInDatabase = await Context.Database.OsuUsers.FindAsync(Context.Update.From!.Id);

            if (osuUserInDatabase is null || !osuUserInDatabase.IsAdmin) return;

            string[] parameters = Context.Update.Text!.GetCommandParameters()!;
            if (parameters[0] == "json")
            {
                string result = JsonConvert.SerializeObject(Context.Update,
                    Formatting.Indented,
                    new JsonSerializerSettings() { NullValueHandling = NullValueHandling.Ignore });
                await Context.Update.EditAsync(Context.BotClient, result);
            }
            else if (parameters[0] == "test")
            {
                Message waitMessage = await Context.Update.ReplyAsync(Context.BotClient, "wait");
                await waitMessage.EditAsync(Context.BotClient, new string('a', (int)Math.Pow(2, 14)));
            }
        }
    }
}