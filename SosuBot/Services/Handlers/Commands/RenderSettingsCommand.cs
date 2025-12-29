using Microsoft.Extensions.DependencyInjection;
using SosuBot.Extensions;
using SosuBot.Helpers;
using SosuBot.Localization;
using SosuBot.Localization.Languages;
using SosuBot.Services.Handlers.Abstract;
using SosuBot.Services.Synchronization;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace SosuBot.Services.Handlers.Commands;

public sealed class RenderSettingsCommand : CommandBase<Message>
{
    public static readonly string[] Commands = ["/settings"];
    private RateLimiterFactory _rateLimiterFactory = null!;

    public override Task BeforeExecuteAsync()
    {
        _rateLimiterFactory = Context.ServiceProvider.GetRequiredService<RateLimiterFactory>();
        return Task.CompletedTask;
    }

    public override async Task ExecuteAsync()
    {
        if (Context.Update.Chat.Id != Context.Update.From?.Id)
        {
            await Context.Update.ReplyAsync(Context.BotClient, "Только в личке с ботом.");
            return;
        }

        await BeforeExecuteAsync();

        var rateLimiter = _rateLimiterFactory.Get(RateLimiterFactory.RateLimitPolicy.Command);
        if (!await rateLimiter.IsAllowedAsync($"{Context.Update.From!.Id}"))
        {
            await Context.Update.ReplyAsync(Context.BotClient, "Давай не так быстро!");
            return;
        }

        ILocalization language = new Russian();

        var osuUserInDatabase = await Context.Database.OsuUsers.FindAsync(Context.Update.From!.Id);
        if (osuUserInDatabase is null)
        {
            await Context.Update.ReplyAsync(Context.BotClient, language.error_userNotSetHimself);
            return;
        }

        // Fake 500ms wait
        await Task.Delay(500);

        string hitErrorMeter = (osuUserInDatabase.RenderSettings.HitErrorMeter ? Emojis.CheckMarkEmoji : "") + "HitErrorMeter";
        string aimErrorMeter = (osuUserInDatabase.RenderSettings.AimErrorMeter ? Emojis.CheckMarkEmoji : "") + "AimErrorMeter";
        string hbBar = (osuUserInDatabase.RenderSettings.HPBar ? Emojis.CheckMarkEmoji : "") + "HP Bar";
        string showPP = (osuUserInDatabase.RenderSettings.ShowPP ? Emojis.CheckMarkEmoji : "") + "Show PP";
        string hitCounter = (osuUserInDatabase.RenderSettings.HitCounter ? Emojis.CheckMarkEmoji : "") + "Hit Counter";
        string ignoreFails = (osuUserInDatabase.RenderSettings.IgnoreFailsInReplays ? Emojis.CheckMarkEmoji : "") + "Ignore Fails";
        string video = (osuUserInDatabase.RenderSettings.Video ? Emojis.CheckMarkEmoji : "") + "Video";
        string storyboard = (osuUserInDatabase.RenderSettings.Storyboard ? Emojis.CheckMarkEmoji : "") + "Storyboard";
        string mods = (osuUserInDatabase.RenderSettings.Mods ? Emojis.CheckMarkEmoji : "") + "Mods";
        string keyOverlay = (osuUserInDatabase.RenderSettings.KeyOverlay ? Emojis.CheckMarkEmoji : "") + "Keys";
        string combo = (osuUserInDatabase.RenderSettings.Combo ? Emojis.CheckMarkEmoji : "") + "Combo";
        var ikm = new InlineKeyboardMarkup(
            new[] {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData($"Общая громкость: {osuUserInDatabase.RenderSettings.GeneralVolume*100:00}%", $"{Context.Update.Chat.Id} rs general-volume")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData($"Музыка: {osuUserInDatabase.RenderSettings.MusicVolume*100:00}%", $"{Context.Update.Chat.Id} rs music-volume"),
                    InlineKeyboardButton.WithCallbackData($"Эффекты: {osuUserInDatabase.RenderSettings.SampleVolume*100:00}%", $"{Context.Update.Chat.Id} rs effects-volume")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData($"Skin: {osuUserInDatabase.RenderSettings.SkinName}", $"{Context.Update.Chat.Id} rs skin 1")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData(hitErrorMeter, $"{Context.Update.Chat.Id} rs hit-error-meter"),
                    InlineKeyboardButton.WithCallbackData(aimErrorMeter, $"{Context.Update.Chat.Id} rs aim-error-meter"),
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData(hbBar, $"{Context.Update.Chat.Id} rs hp-bar"),
                    InlineKeyboardButton.WithCallbackData(showPP, $"{Context.Update.Chat.Id} rs pp"),
                    InlineKeyboardButton.WithCallbackData(hitCounter, $"{Context.Update.Chat.Id} rs hit-counter"),
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData(ignoreFails, $"{Context.Update.Chat.Id} rs ignore-fails"),
                    InlineKeyboardButton.WithCallbackData(video, $"{Context.Update.Chat.Id} rs video"),
                    InlineKeyboardButton.WithCallbackData(storyboard, $"{Context.Update.Chat.Id} rs storyboard"),
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData(mods, $"{Context.Update.Chat.Id} rs mods"),
                    InlineKeyboardButton.WithCallbackData(keyOverlay, $"{Context.Update.Chat.Id} rs key-overlay"),
                    InlineKeyboardButton.WithCallbackData(combo, $"{Context.Update.Chat.Id} rs combo"),
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("Сбросить настройки", $"{Context.Update.Chat.Id} rs reset-settings"),
                },
            }
        );
        await Context.Update.ReplyAsync(Context.BotClient, "Настройки рендера", false, replyMarkup: ikm);
    }
}