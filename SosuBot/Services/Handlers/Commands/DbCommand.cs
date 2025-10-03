using System.Text;
using SosuBot.Database.Extensions;
using SosuBot.Extensions;
using SosuBot.Helpers.OutputText;
using SosuBot.Services.Handlers.Abstract;
using Telegram.Bot.Types;

namespace SosuBot.Services.Handlers.Commands;

public sealed class DbCommand : CommandBase<Message>
{
    public static readonly string[] Commands = ["/db"];

    public override async Task ExecuteAsync()
    {
        var osuUserInDatabase = await Context.Database.OsuUsers.FindAsync(Context.Update.From!.Id);

        if (osuUserInDatabase is null || !osuUserInDatabase.IsAdmin) return;

        var parameters = Context.Update.Text!.GetCommandParameters()!;

        if (parameters[0] == "count")
        {
            int count;
            if (parameters[1] == "users") count = Context.Database.OsuUsers.Count();
            else if (parameters[1] == "chats") count = Context.Database.TelegramChats.Count();
            else if (parameters[1] == "groups") count = Context.Database.TelegramChats.Count(m => m.ChatId < 0);
            else throw new NotImplementedException();

            await Context.Update.ReplyAsync(Context.BotClient, $"{parameters[1]}: {count}");
        }
        else if (parameters[0] == "astext")
        {
            MemoryStream stream;
            if (parameters[1] == "users")
            {
                var tableText = Context.Database.OsuUsers.ToReadfriendlyTableString();
                stream = new MemoryStream(Encoding.Default.GetBytes(tableText));
            }
            else if (parameters[1] == "chats")
            {
                var tableText = Context.Database.TelegramChats.ToReadfriendlyTableString();
                stream = new MemoryStream(Encoding.Default.GetBytes(tableText));
            }
            else
            {
                throw new NotImplementedException();
            }

            await Context.Update.ReplyDocumentAsync(Context.BotClient, InputFile.FromStream(stream, "table.txt"));
        }
        else if (parameters[0] == "query")
        {
            if (Context.Update.From.Id != 728384906) // Shoukko's telegram id
            {
                await Context.Update.ReplyAsync(Context.BotClient, "Пшел отсюда!");
                return;
            }

            var query = string.Join(" ", parameters[1..]);
            var response = Context.Database.RawSqlQuery(query);

            await Context.Update.ReplyDocumentAsync(Context.BotClient,
                new InputFileStream(TextHelper.TextToStream(TextHelper.GetReadfriendlyTable(response))));
        }
        else
        {
            throw new NotImplementedException();
        }
    }
}