﻿using Newtonsoft.Json;
using SosuBot.Extensions;
using SosuBot.Services.Handlers.Abstract;
using Telegram.Bot.Types;

namespace SosuBot.Services.Handlers.Commands;

public class CustomCommand : CommandBase<Message>
{
    public static string[] Commands = ["/c"];

    public override async Task ExecuteAsync()
    {
        var osuUserInDatabase = await Context.Database.OsuUsers.FindAsync(Context.Update.From!.Id);

        if (osuUserInDatabase is null || !osuUserInDatabase.IsAdmin) return;

        var parameters = Context.Update.Text!.GetCommandParameters()!;
        if (parameters[0] == "json")
        {
            var result = JsonConvert.SerializeObject(Context.Update,
                Formatting.Indented,
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            await Context.Update.EditAsync(Context.BotClient, result);
        }
        else if (parameters[0] == "test")
        {
            var waitMessage = await Context.Update.ReplyAsync(Context.BotClient, "wait");
            await waitMessage.EditAsync(Context.BotClient, new string('a', (int)Math.Pow(2, 14)));
        }
    }
}