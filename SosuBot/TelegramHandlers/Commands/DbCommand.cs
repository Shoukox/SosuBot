using Microsoft.Extensions.DependencyInjection;
using SosuBot.Database;
using SosuBot.Database.Extensions;
using SosuBot.Extensions;
using SosuBot.TelegramHandlers.Abstract;
using System.Text;
using Telegram.Bot.Types;

namespace SosuBot.TelegramHandlers.Commands;

public sealed class DbCommand : CommandBase<Message>
{
    private BotContext _database = null!;

    public static readonly string[] Commands = ["/db"];

    public override async Task BeforeExecuteAsync()
    {
        await base.BeforeExecuteAsync();
        _database = Context.ServiceProvider.GetRequiredService<BotContext>();
    }
    public override async Task ExecuteAsync()
    {
        var language = Context.GetLocalization();
        var osuUserInDatabase = await _database.OsuUsers.FindAsync(Context.Update.From!.Id);
        if (osuUserInDatabase is null || !osuUserInDatabase.IsAdmin)
        {
            await Context.Update.ReplyAsync(Context.BotClient, language.admin_accessDenied);
            return;
        }

        var parameters = Context.Update.Text!.GetCommandParameters()!;

        if (parameters[0] == "count")
        {
            int count;
            if (parameters[1] == "users") count = _database.OsuUsers.Count();
            else if (parameters[1] == "chats") count = _database.TelegramChats.Count();
            else if (parameters[1] == "groups") count = _database.TelegramChats.Count(m => m.ChatId < 0);
            else throw new NotImplementedException();

            await Context.Update.ReplyAsync(Context.BotClient, LocalizationMessageHelper.AdminCountFormat(language, parameters[1], $"{count}"));
        }
        else if (parameters[0] == "astext")
        {
            MemoryStream stream;
            if (parameters[1] == "users")
            {
                var tableText = _database.OsuUsers.ToReadfriendlyTableString();
                stream = new MemoryStream(Encoding.Default.GetBytes(tableText));
            }
            else if (parameters[1] == "chats")
            {
                var tableText = _database.TelegramChats.ToReadfriendlyTableString();
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
            var query = string.Join(" ", parameters[1..]);
            var response = _database.RawSqlQuery(query);

            await Context.Update.ReplyDocumentAsync(Context.BotClient,
                new InputFileStream(TextHelper.TextToStream(TextHelper.GetReadfriendlyTable(response))));
        }
        else
        {
            throw new NotImplementedException();
        }
    }
}

