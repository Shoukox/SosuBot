using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.Localization;
using SosuBot.Localization.Languages;
using SosuBot.Services.Handlers.Abstract;
using Telegram.Bot.Types;

namespace SosuBot.Services.Handlers.Commands;

public sealed class OsuChatstatsCommand : CommandBase<Message>
{
    public static readonly string[] Commands = ["/chatstats", "/stats"];

    public override async Task ExecuteAsync()
    {
        if (Context.Update.Chat.Type == Telegram.Bot.Types.Enums.ChatType.Private)
        {
            await Context.Update.ReplyAsync(Context.BotClient, "Только для групп.");
            return;
        }

        ILocalization language = new Russian();
        var chatInDatabase = await Context.Database.TelegramChats.FindAsync(Context.Update.Chat.Id);

        var parameters = Context.Update.Text!.GetCommandParameters()!;

        var waitMessage = await Context.Update.ReplyAsync(Context.BotClient, language.waiting);

        // Fake 500ms wait
        await Task.Delay(500);

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

        var foundChatMembers = new List<OsuUser>();
        chatInDatabase!.ExcludeFromChatstats = chatInDatabase.ExcludeFromChatstats ?? new List<long>();
        foreach (var memberId in chatInDatabase.ChatMembers!)
        {
            var foundMember = await Context.Database.OsuUsers.FindAsync(memberId);
            if (foundMember != null && !chatInDatabase.ExcludeFromChatstats.Contains(foundMember.OsuUserId))
                foundChatMembers.Add(foundMember);
        }

        foundChatMembers = foundChatMembers.DistinctBy(m => m.OsuUserId).OrderByDescending(m => m.GetPP(playmode)).Take(10)
            .ToList();

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