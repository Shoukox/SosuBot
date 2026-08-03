using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OsuApi.BanchoV2;
using OsuApi.BanchoV2.Clients.Users.HttpIO;
using SosuBot.Database;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.Localization;
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
        ILocalization language = Context.GetLocalization();
        TokenBucketRateLimiter rateLimiter = _rateLimiterFactory.Get(RateLimiterFactory.RateLimitPolicy.Command);
        if (!await rateLimiter.IsAllowedAsync($"{Context.Update.From!.Id}"))
        {
            await Context.Update.ReplyAsync(Context.BotClient, language.common_rateLimitSlowDown);
            return;
        }


        TelegramChat? chatInDatabase = await _database.TelegramChats.FindAsync(Context.Update.Chat.Id);

        Message waitMessage = await Context.Update.ReplyAsync(Context.BotClient, language.waiting);

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
                chatInDatabase!.TrackedPlayers = null;
            }

            TrackedPlayerSubscription[] subscriptions = await _database.TrackedPlayerSubscriptions
                .Where(subscription => subscription.ChatId == Context.Update.Chat.Id)
                .ToArrayAsync(Context.CancellationToken);
            _database.TrackedPlayerSubscriptions.RemoveRange(subscriptions);
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
            GetUserResponse? getUserResponse = await _osuApiV2.Users.GetUser("@" + osuUsername, new());
            if (getUserResponse == null)
            {
                await waitMessage.EditAsync(Context.BotClient, language.error_userNotFound + $"\n({osuUsername})");
                return;
            }

            nicknames.Add(getUserResponse.UserExtend!.Username!);
            trackedPlayers.Add(getUserResponse.UserExtend!.Id.Value);
        }

        TrackedPlayerSubscription[] existingSubscriptions = await _database.TrackedPlayerSubscriptions
            .Where(subscription => subscription.ChatId == Context.Update.Chat.Id)
            .ToArrayAsync(Context.CancellationToken);
        _database.TrackedPlayerSubscriptions.RemoveRange(
            existingSubscriptions.Where(subscription => !trackedPlayers.Contains(subscription.PlayerId)));

        HashSet<int> existingPlayerIds = existingSubscriptions
            .Select(subscription => subscription.PlayerId)
            .ToHashSet();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        foreach (int playerId in trackedPlayers.Where(playerId => !existingPlayerIds.Contains(playerId)))
        {
            _database.TrackedPlayerSubscriptions.Add(new TrackedPlayerSubscription
            {
                ChatId = Context.Update.Chat.Id,
                PlayerId = playerId,
                StartedAtUtc = now
            });
        }

        chatInDatabase!.TrackedPlayers = trackedPlayers.ToList();
        await waitMessage.EditAsync(Context.BotClient, LocalizationMessageHelper.TrackNowTrackingPlayers(language, $"{string.Join(", ", nicknames)}"));
    }
}




