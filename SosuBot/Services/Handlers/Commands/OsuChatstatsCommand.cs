using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.Helpers.OsuTypes;
using SosuBot.Localization;
using SosuBot.Services.Handlers.Abstract;
using Telegram.Bot.Types;

namespace SosuBot.Services.Handlers.Commands
{
    public class OsuChatstatsCommand : CommandBase<Message>
    {
        public static string[] Commands = ["/chatstats", "/stats"];

        public override async Task ExecuteAsync()
        {
            ILocalization language = new Russian();
            TelegramChat? chatInDatabase = await Context.Database.TelegramChats.FindAsync(Context.Update.Chat.Id);
            OsuUser? osuUserInDatabase = await Context.Database.OsuUsers.FindAsync(Context.Update.From!.Id);

            List<OsuUser> foundChatMembers = new List<OsuUser>();
            string[] parameters = Context.Update.Text!.GetCommandParameters()!;

            Message waitMessage = await Context.Update.ReplyAsync(Context.BotClient, language.waiting);
            string sendText = language.command_chatstats_title;

            Playmode playmode = Playmode.Osu;
            if (parameters.Length == 1)
            {
                string? ruleset = parameters[0].ParseToRuleset();
                if (ruleset is null)
                {
                    await waitMessage.EditAsync(Context.BotClient, language.error_modeIncorrect);
                    return;
                }

                playmode = ruleset.ParseRulesetToPlaymode();
            }

            chatInDatabase!.ExcludeFromChatstats = chatInDatabase.ExcludeFromChatstats ?? new List<long>();
            foreach (long memberId in chatInDatabase!.ChatMembers!)
            {
                if (memberId == 1097613088)
                {
                    Console.WriteLine();
                }
                OsuUser? foundMember = await Context.Database.OsuUsers.FindAsync(memberId);
                if (foundMember != null && !chatInDatabase.ExcludeFromChatstats.Contains(foundMember.TelegramId))
                {
                    foundChatMembers.Add(foundMember);
                }
            }
            foundChatMembers = foundChatMembers.OrderByDescending(m => m.GetPP(playmode)).Take(10).ToList();

            int i = 1;
            foreach (OsuUser chatMember in foundChatMembers)
            {
                sendText += language.command_chatstats_row.Fill([
                    $"{i}",
                    $"{chatMember.OsuUsername}",
                    $"{chatMember.GetPP(playmode):N0}"]);
                i += 1;
            }
            sendText += language.command_chatstats_end;
            await waitMessage.EditAsync(Context.BotClient, sendText);
        }
    }
}
