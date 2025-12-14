using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OsuApi.V2;
using OsuApi.V2.Users.Models;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.Localization;
using SosuBot.Localization.Languages;
using SosuBot.Services.BackgroundServices;
using SosuBot.Services.Handlers.Abstract;
using System.Linq;
using Telegram.Bot.Types;

namespace SosuBot.Services.Handlers.Commands;

public sealed class TrackCommand : CommandBase<Message>
{
    public static readonly string[] Commands = ["/track"];
    private ApiV2 _osuApiV2 = null!;

    public override Task BeforeExecuteAsync()
    {
        _osuApiV2 = Context.ServiceProvider.GetRequiredService<ApiV2>();
        return Task.CompletedTask;
    }
    public override async Task ExecuteAsync()
    {
        await BeforeExecuteAsync();

        ILocalization language = new Russian();
        var chatInDatabase = await Context.Database.TelegramChats.FindAsync(Context.Update.Chat.Id);

        var waitMessage = await Context.Update.ReplyAsync(Context.BotClient, language.waiting);

        // Fake 500ms wait
        await Task.Delay(500);

        var parameters = Context.Update.Text!.GetCommandParameters()!;
        if (parameters.Length == 0)
        {
            await waitMessage.EditAsync(Context.BotClient, language.error_argsLength + "\n/track [user1..3]\n/track rm");
            return;
        }

        if (parameters.Length == 1 && parameters[0] == "rm")
        {
            if (chatInDatabase!.TrackedPlayers != null)
            {
                List<int> usersToRemoveFromObservedList = [];
                foreach (int osuUserId in chatInDatabase!.TrackedPlayers)
                {
                    if (!Context.Database.TelegramChats.Any(m => m.TrackedPlayers != null && m.TrackedPlayers.Contains(osuUserId)))
                    {
                        usersToRemoveFromObservedList.Add(osuUserId);
                    }
                }

                await ScoresObserverBackgroundService.RemovePlayersFromObserverList(usersToRemoveFromObservedList);
            }
            await waitMessage.EditAsync(Context.BotClient, $"Лист был очищен.");
            return;
        }

        int maxArgsCount = 3;
        if (parameters.Length > maxArgsCount)
        {
            await waitMessage.EditAsync(Context.BotClient, language.error_argsLength + $"\nДопустимо макс. {maxArgsCount} игрока на группу");
            return;
        }

        List<string> nicknames = [];
        HashSet<int> trackedPlayers = new HashSet<int>();
        foreach (string osuUsername in parameters)
        {
            var getUserResponse = await _osuApiV2.Users.GetUser("@" + osuUsername, new());
            if (getUserResponse == null)
            {
                await waitMessage.EditAsync(Context.BotClient, language.error_userNotFound + $"\n({osuUsername})");
                return;
            }

            nicknames.Add(getUserResponse.UserExtend!.Username!);    
            trackedPlayers.Add(getUserResponse.UserExtend!.Id.Value);
        }

        await ScoresObserverBackgroundService.AddPlayersToObserverList(trackedPlayers.ToArray());

        chatInDatabase!.TrackedPlayers = trackedPlayers.ToList();
        await waitMessage.EditAsync(Context.BotClient, $"Теперь в этой группе отслеживаются новые топ скоры (из топ50) следующих игроков:\n{string.Join(", ", nicknames)}");
    }
}