using Microsoft.Extensions.DependencyInjection;
using OsuApi.BanchoV2;
using OsuApi.BanchoV2.Clients.Users.HttpIO;
using OsuApi.BanchoV2.Users.Models;
using SosuBot.Database;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.Helpers;
using SosuBot.Helpers.OutputText;
using SosuBot.Localization;
using SosuBot.Services;
using SosuBot.Services.Synchronization;
using SosuBot.TelegramHandlers.Abstract;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace SosuBot.TelegramHandlers.Callbacks;

/// <summary>The mode buttons under a /userp picture: redraws it for the chosen mode.</summary>
public sealed class OsuUserPictureCallback : CommandBase<CallbackQuery>
{
    public static readonly string Command = UserHelper.UserPictureCallbackCommand;

    public override async Task ExecuteAsync()
    {
        string[] parameters = Context.Update.Data!.Split(' ');
        if (parameters[1] == UserHelper.UserPictureShownMode)
        {
            await Context.Update.AnswerAsync(Context.BotClient);
            return;
        }

        ILocalization language = Context.GetLocalization();
        // Every click draws a picture, unlike the text of /user.
        TokenBucketRateLimiter rateLimiter = Context.ServiceProvider.GetRequiredService<RateLimiterFactory>()
            .Get(RateLimiterFactory.RateLimitPolicy.Command);
        if (!await rateLimiter.IsAllowedAsync($"{Context.Update.From.Id}"))
        {
            await Context.Update.AnswerAsync(Context.BotClient, language.common_rateLimitSlowDown);
            return;
        }

        var playmode = (Playmode)int.Parse(parameters[1]);
        string username = string.Join(' ', parameters[2..]);

        BanchoApiV2 osuApi = Context.ServiceProvider.GetRequiredService<BanchoApiV2>();
        GetUserResponse? response = await osuApi.Users.GetUser($"@{username}", new GetUserQueryParameters(),
            playmode.ToRuleset());
        if (response?.UserExtend is not { } user)
        {
            await Context.Update.AnswerAsync(Context.BotClient, language.error_userNotFound, showAlert: true);
            return;
        }

        UserProfileDifferenceTexts differences = await UserHelper.GetProfileDifferenceTextsAsync(
            Context.ServiceProvider.GetRequiredService<BotContext>(), user, playmode);
        await using MemoryStream picture = await Context.ServiceProvider
            .GetRequiredService<UserProfileCardService>()
            .GenerateAsync(user, playmode, differences, Context.CancellationToken);

        Message message = Context.Update.Message!;
        try
        {
            await Context.BotClient.EditMessageMedia(
                message.Chat.Id,
                message.MessageId,
                new InputMediaPhoto(new InputFileStream(picture, "profile.png"))
                {
                    Caption = UserProfileWidget.BuildCaption(user, playmode,
                        Context.ServiceProvider.GetRequiredService<ScoreHelper>()),
                    ParseMode = ParseMode.Html
                },
                UserHelper.BuildUserPictureKeyboard(user.Username!, playmode),
                cancellationToken: Context.CancellationToken);
        }
        catch (ApiRequestException exception) when (exception.ErrorCode == 400)
        {
            // Two quick clicks on the same button: the message already shows this mode.
        }

        await Context.Update.AnswerAsync(Context.BotClient);
    }
}
