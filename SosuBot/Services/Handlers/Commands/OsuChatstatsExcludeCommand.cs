using Microsoft.EntityFrameworkCore;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.Localization;
using SosuBot.Services.Handlers.Abstract;
using Telegram.Bot.Types;

namespace SosuBot.Services.Handlers.Commands
{
    public class OsuChatstatsExcludeCommand : CommandBase<Message>
    {
        public static string[] Commands = ["/exclude"];

        public override async Task ExecuteAsync()
        {
            ILocalization language = new Russian();
            TelegramChat? chatInDatabase = await Context.Database.TelegramChats.FindAsync(Context.Update.Chat.Id);

            Message waitMessage = await Context.Update.ReplyAsync(Context.BotClient, language.waiting);
            string[] parameters = Context.Update.Text!.GetCommandParameters()!;
            if (parameters.Length == 0)
            {
                await Context.Update.ReplyAsync(Context.BotClient, language.error_nameIsEmpty);
                return;
            }

            string osuUsernameToExclude = parameters[0];
            OsuUser? osuUserToExclude = await Context.Database.OsuUsers.FirstOrDefaultAsync(m => m.OsuUsername.Trim().ToLowerInvariant() == osuUsernameToExclude.Trim().ToLowerInvariant());
            if (osuUserToExclude is null)
            {
                await Context.Update.ReplyAsync(Context.BotClient, language.error_userNotFoundInBotsDatabase);
                return;
            }

            chatInDatabase!.ExcludeFromChatstats = chatInDatabase.ExcludeFromChatstats ?? new List<long>();
            chatInDatabase!.ChatMembers = chatInDatabase.ChatMembers ?? new List<long>();
            if (chatInDatabase.ExcludeFromChatstats.Contains(osuUserToExclude.TelegramId))
            {
                await Context.Update.ReplyAsync(Context.BotClient, language.error_excludeListAlreadyContainsThisId);
                return;
            }

            chatInDatabase.ExcludeFromChatstats.Add(osuUserToExclude!.TelegramId);
            string sendText = language.command_excluded.Fill([osuUsernameToExclude]);
            await waitMessage.EditAsync(Context.BotClient, sendText);
        }
    }
}
