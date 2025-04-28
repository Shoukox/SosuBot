using Microsoft.EntityFrameworkCore;
using Sosu.Localization;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using Telegram.Bot.Types;

namespace SosuBot.Services.Handlers.MessageCommands
{
    public class OsuChatstatsIncludeCommand : CommandBase<Message>
    {
        public static string[] Commands = ["/include"];

        public override async Task ExecuteAsync()
        {
            ILocalization language = new Russian();
            TelegramChat? chatInDatabase = await Database.TelegramChats.FindAsync(Context.Chat.Id);

            Message waitMessage = await Context.ReplyAsync(BotClient, language.waiting);
            string[] parameters = Context.Text!.GetCommandParameters()!;
            if (parameters.Length == 0)
            {
                await Context.ReplyAsync(BotClient, language.error_nameIsEmpty);
                return;
            }

            string osuUsernameToExclude = parameters[0];
            OsuUser? osuUserToExclude = await Database.OsuUsers.FirstOrDefaultAsync(m => m.OsuUsername.Trim().ToLowerInvariant() == osuUsernameToExclude.Trim().ToLowerInvariant());
            if (osuUserToExclude is null)
            {
                await Context.ReplyAsync(BotClient, language.error_userNotFoundInBotsDatabase);
                return;
            }

            chatInDatabase!.ExcludeFromChatstats = chatInDatabase.ExcludeFromChatstats ?? new List<long>();
            chatInDatabase!.ChatMembers = chatInDatabase.ChatMembers ?? new List<long>();
            if (!chatInDatabase.ExcludeFromChatstats.Contains(osuUserToExclude.TelegramId))
            {
                await Context.ReplyAsync(BotClient, language.error_userWasNotExcluded);
                return;
            }

            chatInDatabase.ExcludeFromChatstats.Remove(osuUserToExclude!.TelegramId);
            string sendText = language.command_included.Fill([osuUsernameToExclude]);
            await waitMessage.EditAsync(BotClient, sendText);
        }
    }
}
