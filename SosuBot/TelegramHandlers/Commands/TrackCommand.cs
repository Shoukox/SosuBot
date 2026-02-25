using Microsoft.Extensions.DependencyInjection;
using OsuApi.BanchoV2;
using SosuBot.Database;
using SosuBot.Extensions;
using SosuBot.Services.BackgroundServices;
using SosuBot.Services.Synchronization;
using SosuBot.TelegramHandlers.Abstract;
using Telegram.Bot.Types;

namespace SosuBot.TelegramHandlers.Commands;

public sealed class TrackCommand : CommandBase<Message>
{
    public static readonly string[] Commands = ["/track"];
    private BanchoApiV2 _osuApiV2 = null!;
    private RateLimiterFactory _rateLimiterFactory = null!;
    private BotContext _database = null!;

    public override async Task BeforeExecuteAsync()
    {
        await base.BeforeExecuteAsync();
        _osuApiV2 = Context.ServiceProvider.GetRequiredService<BanchoApiV2>();
        _rateLimiterFactory = Context.ServiceProvider.GetRequiredService<RateLimiterFactory>();
        _database = Context.ServiceProvider.GetRequiredService<BotContext>();
    }
    public override async Task ExecuteAsync()
    {
        var language = Context.GetLocalization();
        var rateLimiter = _rateLimiterFactory.Get(RateLimiterFactory.RateLimitPolicy.Command);
        if (!await rateLimiter.IsAllowedAsync($"{Context.Update.From!.Id}"))
        {
            await Context.Update.ReplyAsync(Context.BotClient, language.common_rateLimitSlowDown);
            return;
        }


        var chatInDatabase = await _database.TelegramChats.FindAsync(Context.Update.Chat.Id);

        var waitMessage = await Context.Update.ReplyAsync(Context.BotClient, language.waiting);

        // Fake 500ms wait
        await Task.Delay(500);

        var parameters = Context.Update.Text!.GetCommandParameters()!;
        if (parameters.Length == 0)
        {
            await waitMessage.EditAsync(Context.BotClient, language.error_argsLength + $"\n{language.track_usage}");
            return;
        }

        if (parameters.Length == 1 && parameters[0] == "rm")
        {
            if (chatInDatabase!.TrackedPlayers != null)
            {
                List<int> usersToRemoveFromObservedList = [];
                foreach (int osuUserId in chatInDatabase!.TrackedPlayers)
                {
                    if (!_database.TelegramChats.Any(m => m.TrackedPlayers != null && m.TrackedPlayers.Contains(osuUserId)))
                    {
                        usersToRemoveFromObservedList.Add(osuUserId);
                    }
                }

                await ScoresObserverBackgroundService.RemovePlayersFromObserverList(usersToRemoveFromObservedList);
            }
            await waitMessage.EditAsync(Context.BotClient, language.track_cleared);
            return;
        }

        int maxArgsCount = 3;
        if (parameters.Length > maxArgsCount)
        {
            await waitMessage.EditAsync(Context.BotClient, language.error_argsLength + "\n" + LocalizationMessageHelper.TrackMaxPlayersPerGroup(language, $"{maxArgsCount}"));
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
        await waitMessage.EditAsync(Context.BotClient, LocalizationMessageHelper.TrackNowTrackingPlayers(language, $"{string.Join(", ", nicknames)}"));
    }
}




