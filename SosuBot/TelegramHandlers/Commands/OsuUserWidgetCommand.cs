using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OsuApi.BanchoV2.Clients.Users.HttpIO;
using OsuApi.BanchoV2.Users.Models;
using SosuBot.Configuration;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.Graphics;
using SosuBot.Helpers;
using SosuBot.Localization;
using SosuBot.Services;
using SosuBot.Services.Synchronization;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace SosuBot.TelegramHandlers.Commands;

/// <summary>
///     /user as a Telegram HTML widget: all four modes are packed into the widget state, so switching tabs is
///     instant and needs no requests to the bot. In production Telegram delivers widgets to private chats only.
/// </summary>
public sealed class OsuUserWidgetCommand : OsuUserCommand
{
    public new static readonly string[] Commands = ["/userw"];
    public new static readonly string Description = "[user] профиль игрока в виде виджета";

    private BotConfiguration _botConfig = null!;
    private OsuProfileImageService _profileImages = null!;
    private ILogger<OsuUserWidgetCommand> _logger = null!;

    public override async Task BeforeExecuteAsync()
    {
        await base.BeforeExecuteAsync();
        _botConfig = Context.ServiceProvider.GetRequiredService<IOptions<BotConfiguration>>().Value;
        _profileImages = Context.ServiceProvider.GetRequiredService<OsuProfileImageService>();
        _logger = Context.ServiceProvider.GetRequiredService<ILogger<OsuUserWidgetCommand>>();
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

        if (Context.Update.Chat.Type != ChatType.Private)
        {
            await Context.Update.ReplyAsync(Context.BotClient,
                language.userWidget_privateOnly.Fill([$"https://t.me/{_botConfig.Username}"]));
            return;
        }

        OsuUser? osuUserInDatabase = await _database.OsuUsers.FindAsync(Context.Update.From!.Id);
        Message waitMessage = await Context.Update.ReplyAsync(Context.BotClient, language.waiting);

        if (await LookupUserAsync(language, osuUserInDatabase, waitMessage) is not { } lookup)
            return;

        UserExtend user = lookup.User;
        UserProfileDifferenceTexts differences =
            await UserHelper.GetProfileDifferenceTextsAsync(_database, user, lookup.Playmode);
        UserHelper.UpdateOsuUsers(_database, user, lookup.Playmode);

        Task<byte[]?> avatarTask = _profileImages.DownloadAvatarAsync(user, Context.CancellationToken);
        Task<byte[]?> bannerTask = _profileImages.DownloadBannerAsync(user, Context.CancellationToken);
        UserStatistics?[] statisticsByMode = await Task.WhenAll(UserProfileWidget.Modes.Select(mode =>
            mode == lookup.Playmode
                ? Task.FromResult(user.Statistics)
                : GetStatisticsAsync(user.Id!.Value, mode)));
        byte[]? avatar = await avatarTask;
        byte[]? banner = await bannerTask;

        string html = UserProfileWidget.BuildHtml(
            language,
            WidgetArtwork.EncodeAvatarCandidates(avatar),
            WidgetArtwork.EncodeBanner(banner),
            WidgetArtwork.GetAccentColors(avatar));

        try
        {
            await Context.BotClient.SendWidget(
                Context.Update.Chat.Id,
                html,
                publicData: UserProfileWidget.BuildPublicData(user, lookup.Playmode, statisticsByMode, differences),
                caption: UserProfileWidget.BuildCaption(user, lookup.Playmode, _scoreHelper),
                parseMode: ParseMode.Html,
                replyParameters: new ReplyParameters { MessageId = Context.Update.MessageId },
                cancellationToken: Context.CancellationToken);
        }
        catch (ApiRequestException exception)
        {
            // An outdated self-hosted Bot API server or a widget limit violation lands here.
            _logger.LogWarning(exception, "Failed to send the /userw widget");
            await waitMessage.EditAsync(Context.BotClient, language.userWidget_failed);
            return;
        }

        await Context.BotClient.DeleteMessage(waitMessage.Chat.Id, waitMessage.Id, Context.CancellationToken);
    }

    private async Task<UserStatistics?> GetStatisticsAsync(int userId, Playmode mode)
    {
        try
        {
            GetUserResponse? response = await _osuApiV2.Users.GetUser(
                userId.ToString(), new GetUserQueryParameters(), mode.ToRuleset());
            return response?.UserExtend?.Statistics;
        }
        catch (HttpRequestException exception)
        {
            // One missing mode only disables its tab.
            _logger.LogWarning(exception, "Failed to fetch {Mode} statistics for osu! user {UserId}", mode, userId);
            return null;
        }
    }
}
