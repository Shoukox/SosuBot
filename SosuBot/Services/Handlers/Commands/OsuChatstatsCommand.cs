using Sosu.Localization;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.OsuTypes;
using Telegram.Bot.Types;

namespace SosuBot.Services.Handlers.Commands
{
    public class OsuChatstatsCommand : CommandBase<Message>
    {
        public static string[] Commands = ["/chatstats", "/stats"];

        public override async Task ExecuteAsync()
        {
            ILocalization language = new Russian();
            TelegramChat? chatInDatabase = await Database.TelegramChats.FindAsync(Context.Chat.Id);
            OsuUser? osuUserInDatabase = await Database.OsuUsers.FindAsync(Context.From!.Id);
            List<OsuUser> foundChatMembers = new List<OsuUser>();
            string[] parameters = Context.Text!.GetCommandParameters()!;

            Message waitMessage = await Context.ReplyAsync(BotClient, language.waiting);
            string sendText = language.command_chatstats_title;

            Playmode playmode = Playmode.Osu;
            if (parameters.Length == 1)
            {
                string? ruleset = parameters[0].ParseToRuleset();
                if (ruleset is null)
                {
                    await waitMessage.EditAsync(BotClient, language.error_modeIncorrect);
                    return;
                }

                playmode = ruleset.ParseRulesetToPlaymode();
            }

            chatInDatabase!.ExcludeFromChatstats = chatInDatabase.ExcludeFromChatstats ?? new List<long>();
            foreach (var memberId in chatInDatabase!.ChatMembers!)
            {
                OsuUser? foundMember = await Database.OsuUsers.FindAsync(memberId);
                if (foundMember != null && !chatInDatabase.ExcludeFromChatstats.Contains(foundMember.TelegramId)) foundChatMembers.Add(foundMember);
            }
            foundChatMembers = foundChatMembers.Take(10).OrderByDescending(m => m.GetPP(playmode)).ToList();

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
            await waitMessage.EditAsync(BotClient, sendText);
        }
    }
}
