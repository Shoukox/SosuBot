using OsuApi.Core.V2.Models;
using SosuBot.Database.Extensions;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.Helpers.OsuTypes;
using SosuBot.Localization;
using SosuBot.Services.Handlers.Abstract;
using System.IO;
using System.Text;
using System.Text.Json;
using Telegram.Bot.Types;

namespace SosuBot.Services.Handlers.Commands
{
    public class DbCommand : CommandBase<Message>
    {
        public static string[] Commands = ["/db"];

        public override async Task ExecuteAsync()
        {
            ILocalization language = new Russian();
            TelegramChat? chatInDatabase = await Context.Database.TelegramChats.FindAsync(Context.Update.Chat.Id);
            OsuUser? osuUserInDatabase = await Context.Database.OsuUsers.FindAsync(Context.Update.From!.Id);

            if (osuUserInDatabase is null || !osuUserInDatabase.IsAdmin) return;

            string[] parameters = Context.Update.Text!.GetCommandParameters()!;

            if (parameters[0] == "fromfiles")
            {
                int startCount = Context.Database.OsuUsers.Count();
                string osuusers = "osuusers.txt";
                var users = JsonSerializer.Deserialize<List<Legacy.osuUser>>(File.ReadAllText(osuusers));
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
                await Context.Update.ReplyAsync(Context.BotClient, $"Added {Context.Database.OsuUsers.Count() - startCount} new users");
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
                await Context.Update.ReplyAsync(Context.BotClient, $"Added {Context.Database.OsuUsers.Count() - startCount} new users");
            }
            else if (parameters[0] == "count")
            {
                int count = 0;
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
                else throw new NotImplementedException();

                await Context.Update.ReplyDocumentAsync(Context.BotClient, InputFile.FromStream(stream, "table.txt"));
            }
            else throw new NotImplementedException();
        }
    }
}
