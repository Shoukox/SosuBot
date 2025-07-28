using OsuApi.V2.Models;
using SosuBot.Database.Extensions;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.Services.Handlers.Abstract;
using System.Text;
using System.Text.Json;
using SosuBot.Helpers.OutputText;
using SosuBot.Helpers.Types;
using Telegram.Bot.Types;

namespace SosuBot.Services.Handlers.Commands
{
    public class DbCommand : CommandBase<Message>
    {
        public static readonly string[] Commands = ["/db"];

        public override async Task ExecuteAsync()
        {
            OsuUser? osuUserInDatabase = await Context.Database.OsuUsers.FindAsync(Context.Update.From!.Id);

            if (osuUserInDatabase is null || !osuUserInDatabase.IsAdmin) return;

            string[] parameters = Context.Update.Text!.GetCommandParameters()!;

            if (parameters[0] == "fromfiles")
            {
                int startCount = Context.Database.OsuUsers.Count();
                string osuusers = "osuusers.txt";
                var users = JsonSerializer.Deserialize<List<Legacy.osuUser>>(await File.ReadAllTextAsync(osuusers));
                if (users == null)
                {
                    await Context.Update.ReplyAsync(Context.BotClient, "Incorrect json");
                    return;
                }

                foreach (var user in users)
                {
                    var response = await Context.OsuApiV2.Users.GetUser($"@{user.osuName}", new(), Ruleset.Osu);
                    if (response is null) continue;
                    var osuUserFromApi = response.UserExtend!;
                    OsuUser osuUser = new OsuUser()
                    {
                        OsuMode = Playmode.Osu,
                        OsuUserId = osuUserFromApi.Id.Value,
                        OsuUsername = osuUserFromApi.Username!,
                        TelegramId = user.telegramId,
                    };
                    if (!Context.Database.OsuUsers.Any(m => m.TelegramId == osuUser.TelegramId))
                    {
                        await Context.Database.OsuUsers.AddAsync(osuUser);
                    }
                }

                await Context.Database.SaveChangesAsync();
                await Context.Update.ReplyAsync(Context.BotClient,
                    $"Added {Context.Database.OsuUsers.Count() - startCount} new users");
            }
            else if (parameters[0] == "fromtext")
            {
                int startCount = Context.Database.OsuUsers.Count();
                var users = JsonSerializer.Deserialize<List<Legacy.osuUser>>(string.Join(" ", parameters[2..]));
                if (users == null)
                {
                    await Context.Update.ReplyAsync(Context.BotClient, "Incorrect json");
                    return;
                }

                foreach (var user in users)
                {
                    var response = await Context.OsuApiV2.Users.GetUser($"@{user.osuName}", new(), Ruleset.Osu);
                    if (response is null) continue;
                    var osuUserFromApi = response.UserExtend!;
                    OsuUser osuUser = new OsuUser()
                    {
                        OsuMode = Playmode.Osu,
                        OsuUserId = osuUserFromApi.Id.Value,
                        OsuUsername = osuUserFromApi.Username!,
                        TelegramId = user.telegramId,
                    };
                    if (!Context.Database.OsuUsers.Any(m => m.TelegramId == osuUser.TelegramId))
                    {
                        await Context.Database.OsuUsers.AddAsync(osuUser);
                    }
                }

                await Context.Database.SaveChangesAsync();
                await Context.Update.ReplyAsync(Context.BotClient,
                    $"Added {Context.Database.OsuUsers.Count() - startCount} new users");
            }
            else if (parameters[0] == "count")
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
                    string tableText = Context.Database.OsuUsers.ToReadfriendlyTableString();
                    stream = new MemoryStream(Encoding.Default.GetBytes(tableText));
                }
                else if (parameters[1] == "chats")
                {
                    string tableText = Context.Database.TelegramChats.ToReadfriendlyTableString();
                    stream = new MemoryStream(Encoding.Default.GetBytes(tableText));
                }
                else throw new NotImplementedException();

                await Context.Update.ReplyDocumentAsync(Context.BotClient, InputFile.FromStream(stream, "table.txt"));
            }
            else if (parameters[0] == "query")
            {
                if (Context.Update.From.Id != 728384906) // Shoukko's telegram id
                {
                    await Context.Update.ReplyAsync(Context.BotClient, "Пшел отсюда!");
                    return;
                }

                string query = string.Join(" ", parameters[1..]);
                List<List<string>> response = Context.Database.RawSqlQuery(query);

                await Context.Update.ReplyDocumentAsync(Context.BotClient,
                    new InputFileStream(TextHelper.TextToStream(TextHelper.GetReadfriendlyTable(response))));
            }
            else throw new NotImplementedException();
        }
    }
}