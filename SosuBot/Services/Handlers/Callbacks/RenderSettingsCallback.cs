using Microsoft.Extensions.DependencyInjection;
using OsuApi.V2;
using SosuBot.Database;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.Helpers;
using SosuBot.Localization;
using SosuBot.Localization.Languages;
using SosuBot.Services.Handlers.Abstract;
using System.Globalization;
using System.Net.Sockets;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace SosuBot.Services.Handlers.Callbacks;

public class RenderSettingsCallback() : CommandBase<CallbackQuery>
{
    public static readonly string Command = "rs";
    private ReplayRenderService _replayRenderService = null!;

    private long _chatId;
    private OsuUser _osuUser = null!;

    public override Task BeforeExecuteAsync()
    {
        _replayRenderService = Context.ServiceProvider.GetRequiredService<ReplayRenderService>();
        return Task.CompletedTask;
    }

    public override async Task ExecuteAsync()
    {
        await BeforeExecuteAsync();

        ILocalization language = new Russian();

        var parameters = Context.Update.Data!.Split(' ');
        _chatId = long.Parse(parameters[0]);
        var settingName = parameters[2];
        string? settingValue = null;
        if (parameters.Length >= 3)
        {
            settingValue = string.Join(" ", parameters[3..]);
        }

        if (_chatId != Context.Update.From.Id)
        {
            await Context.Update.AnswerAsync(Context.BotClient);
            return;
        }

        var osuUser = await Context.Database.OsuUsers.FindAsync(_chatId);
        if (osuUser == null)
        {
            await Context.Update.AnswerAsync(Context.BotClient, language.error_userNotSetHimself);
            return;
        }
        _osuUser = osuUser;

        Func<Task> executeTask = settingName switch
        {
            "general-volume" => GeneralVolume,
            "music-volume" => MusicVolume,
            "effects-volume" => EffectsVolume,
            "skin" => () => Skin(settingValue),
            "hit-error-meter" => HitErrorMeter,
            "aim-error-meter" => AimErrorMeter,
            "hp-bar" => HpBar,
            "pp" => ShowPP,
            "hit-counter" => HitCounter,
            "ignore-fails" => IgnoreFails,
            "video" => Video,
            "storyboard" => Storyboard,
            "mods" => Mods,
            "key-overlay" => KeyOverlay,
            "combo" => Combo,
            "home" => Home,
            "reset-settings" => ResetSettings,
            // setters
            "set-general-volume" => () => SetGeneralVolume(settingValue),
            "set-music-volume" => () => SetMusicVolume(settingValue),
            "set-effects-volume" => () => SetEffectsVolume(settingValue),
            "set-skin" => () => SetSkin(settingValue),
            "set-custom-skin" => () => SetCustomSkin(settingValue),
            _ => () => Task.CompletedTask
        };

        await executeTask();
    }
    async Task ResetSettings()
    {
        _osuUser.RenderSettings = new();
        await Home();
    }
    async Task Home()
    {
        string hitErrorMeter = (_osuUser.RenderSettings.HitErrorMeter ? Emojis.CheckMarkEmoji : "") + "HitErrorMeter";
        string aimErrorMeter = (_osuUser.RenderSettings.AimErrorMeter ? Emojis.CheckMarkEmoji : "") + "AimErrorMeter";
        string hbBar = (_osuUser.RenderSettings.HPBar ? Emojis.CheckMarkEmoji : "") + "HP Bar";
        string showPP = (_osuUser.RenderSettings.ShowPP ? Emojis.CheckMarkEmoji : "") + "Show PP";
        string hitCounter = (_osuUser.RenderSettings.HitCounter ? Emojis.CheckMarkEmoji : "") + "Hit Counter";
        string ignoreFails = (_osuUser.RenderSettings.IgnoreFailsInReplays ? Emojis.CheckMarkEmoji : "") + "Ignore Fails";
        string video = (_osuUser.RenderSettings.Video ? Emojis.CheckMarkEmoji : "") + "Video";
        string storyboard = (_osuUser.RenderSettings.Storyboard ? Emojis.CheckMarkEmoji : "") + "Storyboard";
        string mods = (_osuUser.RenderSettings.Mods ? Emojis.CheckMarkEmoji : "") + "Mods";
        string keyOverlay = (_osuUser.RenderSettings.KeyOverlay ? Emojis.CheckMarkEmoji : "") + "Keys";
        string combo = (_osuUser.RenderSettings.Combo ? Emojis.CheckMarkEmoji : "") + "Combo";
        var ikm = new InlineKeyboardMarkup(
            new[] {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData($"Общая громкость: {_osuUser.RenderSettings.GeneralVolume*100:00}%", $"{_chatId} rs general-volume")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData($"Музыка: {_osuUser.RenderSettings.MusicVolume*100:00}%", $"{_chatId} rs music-volume"),
                    InlineKeyboardButton.WithCallbackData($"Эффекты: {_osuUser.RenderSettings.SampleVolume*100:00}%", $"{_chatId} rs effects-volume")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData($"Skin: {_osuUser.RenderSettings.SkinName}", $"{_chatId} rs skin 1")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData(hitErrorMeter, $"{_chatId} rs hit-error-meter"),
                    InlineKeyboardButton.WithCallbackData(aimErrorMeter, $"{_chatId} rs aim-error-meter"),
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData(hbBar, $"{_chatId} rs hp-bar"),
                    InlineKeyboardButton.WithCallbackData(showPP, $"{_chatId} rs pp"),
                    InlineKeyboardButton.WithCallbackData(hitCounter, $"{_chatId} rs hit-counter"),
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData(ignoreFails, $"{_chatId} rs ignore-fails"),
                    InlineKeyboardButton.WithCallbackData(video, $"{_chatId} rs video"),
                    InlineKeyboardButton.WithCallbackData(storyboard, $"{_chatId} rs storyboard"),
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData(mods, $"{_chatId} rs mods"),
                    InlineKeyboardButton.WithCallbackData(keyOverlay, $"{_chatId} rs key-overlay"),
                    InlineKeyboardButton.WithCallbackData(combo, $"{_chatId} rs combo"),
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("Сбросить настройки", $"{_chatId} rs reset-settings"),
                },
            }
        );

        await Context.BotClient.EditMessageText(_chatId, Context.Update.Message!.Id, "Настройки рендера", replyMarkup: ikm);
    }

    async Task GeneralVolume()
    {
        double usersGeneralVolume = _osuUser.RenderSettings.GeneralVolume;
        string[] percentage = ["10%", "20%", "30%", "40%", "50%", "60%", "70%", "80%", "90%", "100%"];
        int index = (int)Math.Round(usersGeneralVolume / 0.1) - 1;
        percentage[index] = Emojis.CheckMarkEmoji + percentage[index];

        var ikm = new InlineKeyboardMarkup(
            [
                [
                    InlineKeyboardButton.WithCallbackData(percentage[0], $"{_chatId} rs set-general-volume 0.1"),
                    InlineKeyboardButton.WithCallbackData(percentage[1], $"{_chatId} rs set-general-volume 0.2"),
                    InlineKeyboardButton.WithCallbackData(percentage[2], $"{_chatId} rs set-general-volume 0.3"),
                    InlineKeyboardButton.WithCallbackData(percentage[3], $"{_chatId} rs set-general-volume 0.4"),
                    InlineKeyboardButton.WithCallbackData(percentage[4], $"{_chatId} rs set-general-volume 0.5"),
                ],
                [
                    InlineKeyboardButton.WithCallbackData(percentage[5], $"{_chatId} rs set-general-volume 0.6"),
                    InlineKeyboardButton.WithCallbackData(percentage[6], $"{_chatId} rs set-general-volume 0.7"),
                    InlineKeyboardButton.WithCallbackData(percentage[7], $"{_chatId} rs set-general-volume 0.8"),
                    InlineKeyboardButton.WithCallbackData(percentage[8], $"{_chatId} rs set-general-volume 0.9"),
                    InlineKeyboardButton.WithCallbackData(percentage[9], $"{_chatId} rs set-general-volume 1.0"),
                ],
                [
                    InlineKeyboardButton.WithCallbackData("Назад", $"{_chatId} rs home"),
                ],
            ]
        );

        await Context.BotClient.EditMessageText(_chatId, Context.Update.Message!.Id, "Общая громкость", replyMarkup: ikm);
    }

    async Task SetGeneralVolume(string? settingValue)
    {
        if (settingValue is null) throw new NullReferenceException();

        _osuUser.RenderSettings.GeneralVolume = double.Parse(settingValue, CultureInfo.InvariantCulture);
        await GeneralVolume();
    }

    async Task MusicVolume()
    {
        double usersMusicVolume = _osuUser.RenderSettings.MusicVolume;
        string[] percentage = ["10%", "20%", "30%", "40%", "50%", "60%", "70%", "80%", "90%", "100%"];
        int index = (int)Math.Round(usersMusicVolume / 0.1) - 1;
        percentage[index] = Emojis.CheckMarkEmoji + percentage[index];

        var ikm = new InlineKeyboardMarkup(
            [
                [
                    InlineKeyboardButton.WithCallbackData(percentage[0], $"{_chatId} rs set-music-volume 0.1"),
                    InlineKeyboardButton.WithCallbackData(percentage[1], $"{_chatId} rs set-music-volume 0.2"),
                    InlineKeyboardButton.WithCallbackData(percentage[2], $"{_chatId} rs set-music-volume 0.3"),
                    InlineKeyboardButton.WithCallbackData(percentage[3], $"{_chatId} rs set-music-volume 0.4"),
                    InlineKeyboardButton.WithCallbackData(percentage[4], $"{_chatId} rs set-music-volume 0.5"),
                ],
                [
                    InlineKeyboardButton.WithCallbackData(percentage[5], $"{_chatId} rs set-music-volume 0.6"),
                    InlineKeyboardButton.WithCallbackData(percentage[6], $"{_chatId} rs set-music-volume 0.7"),
                    InlineKeyboardButton.WithCallbackData(percentage[7], $"{_chatId} rs set-music-volume 0.8"),
                    InlineKeyboardButton.WithCallbackData(percentage[8], $"{_chatId} rs set-music-volume 0.9"),
                    InlineKeyboardButton.WithCallbackData(percentage[9], $"{_chatId} rs set-music-volume 1.0"),
                ],
                [
                    InlineKeyboardButton.WithCallbackData("Назад", $"{_chatId} rs home"),
                ],
            ]
        );

        await Context.BotClient.EditMessageText(_chatId, Context.Update.Message!.Id, "Громкость музыки", replyMarkup: ikm);
    }

    async Task SetMusicVolume(string? settingValue)
    {
        if (settingValue is null) throw new NullReferenceException();

        _osuUser.RenderSettings.MusicVolume = double.Parse(settingValue, CultureInfo.InvariantCulture);
        await MusicVolume();
    }

    async Task EffectsVolume()
    {
        double usersEffectsVolume = _osuUser.RenderSettings.SampleVolume;
        string[] percentage = ["10%", "20%", "30%", "40%", "50%", "60%", "70%", "80%", "90%", "100%"];
        int index = (int)Math.Round(usersEffectsVolume / 0.1) - 1;
        percentage[index] = Emojis.CheckMarkEmoji + percentage[index];

        var ikm = new InlineKeyboardMarkup(
            [
                [
                    InlineKeyboardButton.WithCallbackData(percentage[0], $"{_chatId} rs set-effects-volume 0.1"),
                    InlineKeyboardButton.WithCallbackData(percentage[1], $"{_chatId} rs set-effects-volume 0.2"),
                    InlineKeyboardButton.WithCallbackData(percentage[2], $"{_chatId} rs set-effects-volume 0.3"),
                    InlineKeyboardButton.WithCallbackData(percentage[3], $"{_chatId} rs set-effects-volume 0.4"),
                    InlineKeyboardButton.WithCallbackData(percentage[4], $"{_chatId} rs set-effects-volume 0.5"),
                ],
                [
                    InlineKeyboardButton.WithCallbackData(percentage[5], $"{_chatId} rs set-effects-volume 0.6"),
                    InlineKeyboardButton.WithCallbackData(percentage[6], $"{_chatId} rs set-effects-volume 0.7"),
                    InlineKeyboardButton.WithCallbackData(percentage[7], $"{_chatId} rs set-effects-volume 0.8"),
                    InlineKeyboardButton.WithCallbackData(percentage[8], $"{_chatId} rs set-effects-volume 0.9"),
                    InlineKeyboardButton.WithCallbackData(percentage[9], $"{_chatId} rs set-effects-volume 1.0"),
                ],
                [
                    InlineKeyboardButton.WithCallbackData("Назад", $"{_chatId} rs home"),
                ],
            ]
        );
        await Context.BotClient.EditMessageText(_chatId, Context.Update.Message!.Id, "Громкость эффектов", replyMarkup: ikm);
    }

    async Task SetEffectsVolume(string? settingValue)
    {
        if (settingValue is null) throw new NullReferenceException();

        _osuUser.RenderSettings.SampleVolume = double.Parse(settingValue, CultureInfo.InvariantCulture);
        await EffectsVolume();
    }

    async Task Skin(string? settingValue)
    {
        if (settingValue is null) throw new NullReferenceException();
        int.TryParse(settingValue, out int page);
        if (page <= 0)
        {
            await Context.Update.AnswerAsync(Context.BotClient);
            return;
        }

        List<string>? availableSkins = null;
        try
        {
            availableSkins = (await _replayRenderService.GetAvailableSkins())!.ToList() ?? [];
        }
        catch (HttpRequestException ex) when (ex.InnerException is SocketException socketException && socketException.ErrorCode == 10061)
        {
            await Context.Update.AnswerAsync(Context.BotClient, "Сервер бота оффлайн. Для кастомного скина используй /setskin");
            return;
        }

        availableSkins.Add("default");
        int totalPages = (int)Math.Ceiling(availableSkins.Count / 7.0);
        availableSkins = availableSkins.Skip((page - 1) * 7).Take(page * 7).ToList();
        if (availableSkins.ToList().Count == 0)
        {
            await Context.Update.AnswerAsync(Context.BotClient);
            return;
        }

        string renderSkinNameNoOsk = _osuUser.RenderSettings.SkinName.EndsWith(".osk") ? _osuUser.RenderSettings.SkinName[..^4] : _osuUser.RenderSettings.SkinName;
        var availableSkinsAsIkm = availableSkins.Select(m => m.EndsWith(".osk") ? m[..^4] : m).Select(m => new InlineKeyboardButton[]
        {
            InlineKeyboardButton.WithCallbackData(renderSkinNameNoOsk == m ? Emojis.CheckMarkEmoji + m : m, $"{_chatId} rs set-skin {page} {m}")
        });

        var ikm = new InlineKeyboardMarkup(
           [.. availableSkinsAsIkm,
            [InlineKeyboardButton.WithCallbackData("<", $"{_chatId} rs skin {page-1}"), InlineKeyboardButton.WithCallbackData($"{page}/{totalPages}", $"{_chatId} dummy"), InlineKeyboardButton.WithCallbackData(">", $"{_chatId} rs skin {page+1}")],
            [InlineKeyboardButton.WithCallbackData("Выбрать свой скин", $"{_chatId} rs set-custom-skin")],
            [InlineKeyboardButton.WithCallbackData("Назад", $"{_chatId} rs home")]
           ]
        );
        await Context.BotClient.EditMessageText(_chatId, Context.Update.Message!.Id, "Выбрать скин", replyMarkup: ikm);
    }

    async Task SetSkin(string? settingValue)
    {
        if (settingValue is null) throw new NullReferenceException();

        string[] splittedSettingValue = settingValue.Split(' ');
        _osuUser.RenderSettings.SkinName = string.Join(' ', splittedSettingValue[1..]) + ".osk";
        await Skin(splittedSettingValue[0]);
    }

    async Task SetCustomSkin(string? settingData)
    {
        await Context.Update.AnswerAsync(Context.BotClient, "Используйте /setskin");
    }

    async Task HitErrorMeter()
    {
        _osuUser.RenderSettings.HitErrorMeter = !_osuUser.RenderSettings.HitErrorMeter;
        await Home();
    }

    async Task AimErrorMeter()
    {
        _osuUser.RenderSettings.AimErrorMeter = !_osuUser.RenderSettings.AimErrorMeter;
        await Home();
    }

    async Task HpBar()
    {
        _osuUser.RenderSettings.HPBar = !_osuUser.RenderSettings.HPBar;
        await Home();
    }

    async Task ShowPP()
    {
        _osuUser.RenderSettings.ShowPP = !_osuUser.RenderSettings.ShowPP;
        await Home();
    }

    async Task HitCounter()
    {
        _osuUser.RenderSettings.HitCounter = !_osuUser.RenderSettings.HitCounter;
        await Home();
    }

    async Task IgnoreFails()
    {
        _osuUser.RenderSettings.IgnoreFailsInReplays = !_osuUser.RenderSettings.IgnoreFailsInReplays;
        await Home();
    }

    async Task Video()
    {
        _osuUser.RenderSettings.Video = !_osuUser.RenderSettings.Video;
        await Home();
    }

    async Task Storyboard()
    {
        _osuUser.RenderSettings.Storyboard = !_osuUser.RenderSettings.Storyboard;
        await Home();
    }

    async Task Mods()
    {
        _osuUser.RenderSettings.Mods = !_osuUser.RenderSettings.Mods;
        await Home();
    }

    async Task KeyOverlay()
    {
        _osuUser.RenderSettings.KeyOverlay = !_osuUser.RenderSettings.KeyOverlay;
        await Home();
    }

    async Task Combo()
    {
        _osuUser.RenderSettings.Combo = !_osuUser.RenderSettings.Combo;
        await Home();
    }
}