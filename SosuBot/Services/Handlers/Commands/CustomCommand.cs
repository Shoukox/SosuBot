using Newtonsoft.Json;
using SosuBot.Extensions;
using SosuBot.Helpers.OutputText;
using SosuBot.Services.Handlers.Abstract;
using Telegram.Bot.Types;

namespace SosuBot.Services.Handlers.Commands;

public class CustomCommand : CommandBase<Message>
{
    public static string[] Commands = ["/c"];

    public override async Task ExecuteAsync()
    {
        var osuUserInDatabase = await Context.Database.OsuUsers.FindAsync(Context.Update.From!.Id);

        if (osuUserInDatabase is null || !osuUserInDatabase.IsAdmin)
        {
            await Context.Update.ReplyAsync(Context.BotClient, "Пшол вон!");
            return;
        }

        var parameters = Context.Update.Text!.GetCommandParameters()!;
        if (parameters[0] == "json")
        {
            var result = JsonConvert.SerializeObject(Context.Update,
                Formatting.Indented,
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

            if (parameters.Length >= 2 && parameters[1] == "text")
            {
                await Context.Update.ReplyAsync(Context.BotClient, result);
            }
            else
            {
                await Context.Update.ReplyDocumentAsync(Context.BotClient, TextHelper.TextToStream(result));
            }
        }
        else if (parameters[0] == "test")
        {
            await Context.Update.ReplyAsync(Context.BotClient, new string('a', (int)Math.Pow(2, 14)));
        }
        else if (parameters[0] == "getuser")
        {
            var osuUserInReply = await Context.Database.OsuUsers.FindAsync(Context.Update.ReplyToMessage!.From!.Id);
            
            var result = JsonConvert.SerializeObject(osuUserInReply,
                Formatting.Indented,
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

            await Context.Update.ReplyAsync(Context.BotClient, result);
        }
    }
}