using Microsoft.Extensions.DependencyInjection;
using OsuApi.V2;
using OsuApi.V2.Users.Models;
using SosuBot.Extensions;
using SosuBot.Localization;
using SosuBot.Localization.Languages;
using SosuBot.Services.BackgroundServices;
using SosuBot.Services.Handlers.Abstract;
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
        var parameters = Context.Update.Text!.GetCommandParameters()!;
        if (parameters.Length == 0)
        {
            await waitMessage.EditAsync(Context.BotClient, language.error_argsLength);
            return;
        }

        int maxArgsCount = 1;
        if (parameters.Length > maxArgsCount)
        {
            await waitMessage.EditAsync(Context.BotClient, language.error_argsLength + $"\nДопустимо макс. {maxArgsCount} игрока на группу");
            return;
        }
        
        HashSet<int> trackedPlayers = new HashSet<int>();
        foreach (string osuUsername in parameters)
        {
            var getUserResponse = await _osuApiV2.Users.GetUser("@"+osuUsername, new());
            if (getUserResponse == null)
            {
                await waitMessage.EditAsync(Context.BotClient, language.error_userNotFound + $"\n ({osuUsername})");
                return;
            }
            
            trackedPlayers.Add(getUserResponse.UserExtend!.Id.Value);
        }

        await ScoresObserverBackgroundService.AddPlayersToObserverList(trackedPlayers.ToArray());
        
        chatInDatabase!.TrackedPlayers = trackedPlayers.ToList();
        await waitMessage.EditAsync(Context.BotClient, $"Теперь в этой группе отслеживаются новые топ скоры (из топ50) следующих игроков:\n{string.Join(", ", parameters)}");

        await Context.Database.SaveChangesAsync();
    }
}