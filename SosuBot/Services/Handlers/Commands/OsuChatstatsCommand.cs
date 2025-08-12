using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.Helpers.Types;
using SosuBot.Localization;
using SosuBot.Localization.Languages;
using SosuBot.Services.Handlers.Abstract;
using Telegram.Bot.Types;

namespace SosuBot.Services.Handlers.Commands;

public class OsuChatstatsCommand : CommandBase<Message>
{
    public static string[] Commands = ["/chatstats", "/stats"];

    public override async Task ExecuteAsync()
    {
        ILocalization language = new Russian();
        var chatInDatabase = await Context.Database.TelegramChats.FindAsync(Context.Update.Chat.Id);
        var osuUserInDatabase = await Context.Database.OsuUsers.FindAsync(Context.Update.From!.Id);

        var foundChatMembers = new List<OsuUser>();
        var parameters = Context.Update.Text!.GetCommandParameters()!;

        var waitMessage = await Context.Update.ReplyAsync(Context.BotClient, language.waiting);

        var playmode = Playmode.Osu;
        if (parameters.Length == 1)
        {
            var ruleset = parameters[0].ParseToRuleset();
            if (ruleset is null)
            {
                await waitMessage.EditAsync(Context.BotClient, language.error_modeIncorrect);
                return;
            }

            playmode = ruleset.ParseRulesetToPlaymode();
        }

        chatInDatabase!.ExcludeFromChatstats = chatInDatabase.ExcludeFromChatstats ?? new List<long>();
        foreach (var memberId in chatInDatabase!.ChatMembers!)
        {
            var foundMember = await Context.Database.OsuUsers.FindAsync(memberId);
            if (foundMember != null && !chatInDatabase.ExcludeFromChatstats.Contains(foundMember.OsuUserId))
                foundChatMembers.Add(foundMember);
        }

        foundChatMembers = foundChatMembers.OrderByDescending(m => m.GetPP(playmode)).Take(10).ToList();

        var sendText = language.command_chatstats_title.Fill([playmode.ToGamemode()]);

        var i = 1;
        foreach (var chatMember in foundChatMembers)
        {
            sendText += language.command_chatstats_row.Fill([
                $"{i}",
                $"{chatMember.OsuUsername}",
                $"{chatMember.GetPP(playmode):N2}"
            ]);
            i += 1;
        }

        sendText += language.command_chatstats_end;
        await waitMessage.EditAsync(Context.BotClient, sendText);
    }
}