using Microsoft.Extensions.DependencyInjection;
using OsuApi.BanchoV2.Users.Models;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.Helpers;
using SosuBot.Helpers.OutputText;
using SosuBot.Localization;
using SosuBot.Services;
using SosuBot.Services.Synchronization;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace SosuBot.TelegramHandlers.Commands;

/// <summary>/user as a picture in the style of the /userw widget, with buttons that switch the game mode.</summary>
public sealed class OsuUserPictureCommand : OsuUserCommand
{
    public new static readonly string[] Commands = ["/userp"];
    public new static readonly string Description = "[user] профиль игрока картинкой";

    protected override string UsageText(ILocalization language) => language.command_userPicture_usage;

    public override async Task ExecuteAsync()
    {
        ILocalization language = Context.GetLocalization();
        TokenBucketRateLimiter rateLimiter = _rateLimiterFactory.Get(RateLimiterFactory.RateLimitPolicy.Command);
        if (!await rateLimiter.IsAllowedAsync($"{Context.Update.From!.Id}"))
        {
            await Context.Update.ReplyAsync(Context.BotClient, language.common_rateLimitSlowDown);
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

        await using MemoryStream picture = await Context.ServiceProvider
            .GetRequiredService<UserProfileCardService>()
            .GenerateAsync(user, lookup.Playmode, differences, Context.CancellationToken);

        await Context.BotClient.EditMessageMedia(
            waitMessage.Chat.Id,
            waitMessage.MessageId,
            new InputMediaPhoto(new InputFileStream(picture, "profile.png"))
            {
                Caption = UserProfileWidget.BuildCaption(user, lookup.Playmode, _scoreHelper),
                ParseMode = ParseMode.Html
            },
            UserHelper.BuildUserPictureKeyboard(user.Username!, lookup.Playmode),
            cancellationToken: Context.CancellationToken);
    }
}
