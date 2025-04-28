using OsuApi.Core.V2.Scores.Models;
using Sosu.Localization;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using System.Text.Json;
using Telegram.Bot.Types;

namespace SosuBot.Services.Handlers.Commands.MessageCommands
{
    public class DbCommand : CommandBase<Message>
    {
        public static string[] Commands = ["/db"];

        public override async Task ExecuteAsync()
        {
            ILocalization language = new Russian();
            TelegramChat? chatInDatabase = await Database.TelegramChats.FindAsync(Context.Chat.Id);
            OsuUser? osuUserInDatabase = await Database.OsuUsers.FindAsync(Context.From!.Id);

            if (osuUserInDatabase is null || !osuUserInDatabase.IsAdmin) return;

            string[] parameters = Context.Text.GetCommandParameters()!;

            if (parameters[0] == "fromfiles")
            {
                int startCount = Database.OsuUsers.Count();
                string osuusers = "osuusers.txt";
                var users = JsonSerializer.Deserialize<List<Legacy.osuUser>>(File.ReadAllText(osuusers));
                foreach (var user in users)
                {
                    var response = (await OsuApiV2.Users.GetUser($"@{user.osuName}", new(), Ruleset.Osu));
                    if (response is null) continue;
                    var osuUserFromApi = response.UserExtend!;
                    OsuUser osuUser = new OsuUser()
                    {
                        OsuMode = OsuTypes.Playmode.Osu,
                        OsuUserId = osuUserFromApi.Id.Value,
                        OsuUsername = osuUserFromApi.Username!,
                        TelegramId = user.telegramId,
                    };
                    if (!Database.OsuUsers.Any(m => m.TelegramId == osuUser.TelegramId))
                    {
                        await Database.OsuUsers.AddAsync(osuUser);
                    }
                }
                await Database.SaveChangesAsync();
                await Context.ReplyAsync(BotClient, $"Added {Database.OsuUsers.Count() - startCount} new users");
            }
            else if (parameters[0] == "fromtext")
            {
                int startCount = Database.OsuUsers.Count();
                var users = JsonSerializer.Deserialize<List<Legacy.osuUser>>(string.Join(" ", parameters[2..]));
                foreach (var user in users)
                {
                    var response = (await OsuApiV2.Users.GetUser($"@{user.osuName}", new(), Ruleset.Osu));
                    if (response is null) continue;
                    var osuUserFromApi = response.UserExtend!;
                    OsuUser osuUser = new OsuUser()
                    {
                        OsuMode = OsuTypes.Playmode.Osu,
                        OsuUserId = osuUserFromApi.Id.Value,
                        OsuUsername = osuUserFromApi.Username!,
                        TelegramId = user.telegramId,
                    };
                    if (!Database.OsuUsers.Any(m => m.TelegramId == osuUser.TelegramId))
                    {
                        await Database.OsuUsers.AddAsync(osuUser);
                    }
                }
                await Database.SaveChangesAsync();
                await Context.ReplyAsync(BotClient, $"Added {Database.OsuUsers.Count() - startCount} new users");
            }
            else if (parameters[0] == "count")
            {
                int count = 0;
                if (parameters[1] == "users") count = Database.OsuUsers.Count();
                else if (parameters[1] == "chats") count = Database.TelegramChats.Count();
                await Context.ReplyAsync(BotClient, $"{parameters[1]}: {count}");
            }
        }
    }
}
