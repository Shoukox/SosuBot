using Microsoft.Extensions.DependencyInjection;
using SosuBot.Database;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.Helpers;
using SosuBot.Helpers.OutputText;
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
    private BotContext _database = null!;

    private long _chatId;
    private OsuUser _osuUser = null!;

    public override async Task BeforeExecuteAsync()
    {
        await base.BeforeExecuteAsync();
        _replayRenderService = Context.ServiceProvider.GetRequiredService<ReplayRenderService>();
        _database = Context.ServiceProvider.GetRequiredService<BotContext>();
    }

    public override async Task ExecuteAsync()
    {
        ILocalization language = new Russian();

        var parameters = Context.Update.Data!.Split(' ');
        var settingName = parameters[1];

        _chatId = Context.Update.Message!.Chat.Id;

        string? settingValue = null;
        if (parameters.Length >= 3)
        {
            settingValue = string.Join(" ", parameters[2..]);
        }

        if (_chatId != Context.Update.From.Id)
        {
            await Context.Update.AnswerAsync(Context.BotClient);
            return;
        }

        var osuUser = await _database.OsuUsers.FindAsync(_chatId);
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
            "background" => BackgroundDim,
            "effects-volume" => EffectsVolume,
            "skin" => () => Skin(settingValue),
            "hit-error-meter" => HitErrorMeter,
            "aim-error-meter" => AimErrorMeter,
            "motion-blur" => MotionBlur,
            "hp-bar" => HpBar,
            "pp" => ShowPP,
            "hit-counter" => HitCounter,
            "ignore-fails" => IgnoreFails,
            "video" => Video,
            "storyboard" => Storyboard,
            "mods" => Mods,
            "key-overlay" => KeyOverlay,
            "combo" => Combo,
            "leaderboard" => Leaderboard,
            "strain-graph" => StrainGraph,
            "home" => Home,
            "reset-settings" => ResetSettings,
            // setters
            "set-general-volume" => () => SetGeneralVolume(settingValue),
            "set-music-volume" => () => SetMusicVolume(settingValue),
            "set-effects-volume" => () => SetEffectsVolume(settingValue),
            "set-background" => () => SetBackgroundDim(settingValue),
            "ss" => () => SetSkin(settingValue),
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
        await Context.BotClient.EditMessageText(_chatId, Context.Update.Message!.Id, "Настройки рендера", replyMarkup: OsuHelper.GetRenderSettingsMarkup(_osuUser.RenderSettings));
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
                    InlineKeyboardButton.WithCallbackData(percentage[0], $"rs set-general-volume 0.1"),
                    InlineKeyboardButton.WithCallbackData(percentage[1], $"rs set-general-volume 0.2"),
                    InlineKeyboardButton.WithCallbackData(percentage[2], $"rs set-general-volume 0.3"),
                    InlineKeyboardButton.WithCallbackData(percentage[3], $"rs set-general-volume 0.4"),
                    InlineKeyboardButton.WithCallbackData(percentage[4], $"rs set-general-volume 0.5"),
                ],
                [
                    InlineKeyboardButton.WithCallbackData(percentage[5], $"rs set-general-volume 0.6"),
                    InlineKeyboardButton.WithCallbackData(percentage[6], $"rs set-general-volume 0.7"),
                    InlineKeyboardButton.WithCallbackData(percentage[7], $"rs set-general-volume 0.8"),
                    InlineKeyboardButton.WithCallbackData(percentage[8], $"rs set-general-volume 0.9"),
                    InlineKeyboardButton.WithCallbackData(percentage[9], $"rs set-general-volume 1.0"),
                ],
                [
                    InlineKeyboardButton.WithCallbackData("Назад", $"rs home"),
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
                    InlineKeyboardButton.WithCallbackData(percentage[0], $"rs set-music-volume 0.1"),
                    InlineKeyboardButton.WithCallbackData(percentage[1], $"rs set-music-volume 0.2"),
                    InlineKeyboardButton.WithCallbackData(percentage[2], $"rs set-music-volume 0.3"),
                    InlineKeyboardButton.WithCallbackData(percentage[3], $"rs set-music-volume 0.4"),
                    InlineKeyboardButton.WithCallbackData(percentage[4], $"rs set-music-volume 0.5"),
                ],
                [
                    InlineKeyboardButton.WithCallbackData(percentage[5], $"rs set-music-volume 0.6"),
                    InlineKeyboardButton.WithCallbackData(percentage[6], $"rs set-music-volume 0.7"),
                    InlineKeyboardButton.WithCallbackData(percentage[7], $"rs set-music-volume 0.8"),
                    InlineKeyboardButton.WithCallbackData(percentage[8], $"rs set-music-volume 0.9"),
                    InlineKeyboardButton.WithCallbackData(percentage[9], $"rs set-music-volume 1.0"),
                ],
                [
                    InlineKeyboardButton.WithCallbackData("Назад", $"rs home"),
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
                    InlineKeyboardButton.WithCallbackData(percentage[0], $"rs set-effects-volume 0.1"),
                    InlineKeyboardButton.WithCallbackData(percentage[1], $"rs set-effects-volume 0.2"),
                    InlineKeyboardButton.WithCallbackData(percentage[2], $"rs set-effects-volume 0.3"),
                    InlineKeyboardButton.WithCallbackData(percentage[3], $"rs set-effects-volume 0.4"),
                    InlineKeyboardButton.WithCallbackData(percentage[4], $"rs set-effects-volume 0.5"),
                ],
                [
                    InlineKeyboardButton.WithCallbackData(percentage[5], $"rs set-effects-volume 0.6"),
                    InlineKeyboardButton.WithCallbackData(percentage[6], $"rs set-effects-volume 0.7"),
                    InlineKeyboardButton.WithCallbackData(percentage[7], $"rs set-effects-volume 0.8"),
                    InlineKeyboardButton.WithCallbackData(percentage[8], $"rs set-effects-volume 0.9"),
                    InlineKeyboardButton.WithCallbackData(percentage[9], $"rs set-effects-volume 1.0"),
                ],
                [
                    InlineKeyboardButton.WithCallbackData("Назад", $"rs home"),
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

    async Task BackgroundDim()
    {
        double usersBackgroundDim = _osuUser.RenderSettings.BackgroundDim;
        string[] percentage = Enumerable.Range(1, 20).Select(m => $"{m * 5}%").ToArray();
        int index = (int)Math.Round(usersBackgroundDim / 0.05) - 1;
        percentage[index] = Emojis.CheckMarkEmoji + percentage[index];

        var ikm = new InlineKeyboardMarkup(
            [
                [
                    InlineKeyboardButton.WithCallbackData(percentage[0], $"rs set-background 0.05"),
                    InlineKeyboardButton.WithCallbackData(percentage[1], $"rs set-background 0.10"),
                    InlineKeyboardButton.WithCallbackData(percentage[2], $"rs set-background 0.15"),
                    InlineKeyboardButton.WithCallbackData(percentage[3], $"rs set-background 0.20"),
                    InlineKeyboardButton.WithCallbackData(percentage[4], $"rs set-background 0.25"),
                ],
                [
                    InlineKeyboardButton.WithCallbackData(percentage[5], $"rs set-background 0.30"),
                    InlineKeyboardButton.WithCallbackData(percentage[6], $"rs set-background 0.35"),
                    InlineKeyboardButton.WithCallbackData(percentage[7], $"rs set-background 0.40"),
                    InlineKeyboardButton.WithCallbackData(percentage[8], $"rs set-background 0.45"),
                    InlineKeyboardButton.WithCallbackData(percentage[9], $"rs set-background 0.50"),
                ],
                [
                    InlineKeyboardButton.WithCallbackData(percentage[10], $"rs set-background 0.55"),
                    InlineKeyboardButton.WithCallbackData(percentage[11], $"rs set-background 0.60"),
                    InlineKeyboardButton.WithCallbackData(percentage[12], $"rs set-background 0.65"),
                    InlineKeyboardButton.WithCallbackData(percentage[13], $"rs set-background 0.70"),
                    InlineKeyboardButton.WithCallbackData(percentage[14], $"rs set-background 0.75"),
                ],
                [
                    InlineKeyboardButton.WithCallbackData(percentage[15], $"rs set-background 0.80"),
                    InlineKeyboardButton.WithCallbackData(percentage[16], $"rs set-background 0.85"),
                    InlineKeyboardButton.WithCallbackData(percentage[17], $"rs set-background 0.90"),
                    InlineKeyboardButton.WithCallbackData(percentage[18], $"rs set-background 0.95"),
                    InlineKeyboardButton.WithCallbackData(percentage[19], $"rs set-background 1.0"),
                ],
                [
                    InlineKeyboardButton.WithCallbackData("Назад", $"rs home"),
                ],
            ]
        );
        await Context.BotClient.EditMessageText(_chatId, Context.Update.Message!.Id, "Затемнение экрана", replyMarkup: ikm);
    }

    async Task SetBackgroundDim(string? settingValue)
    {
        if (settingValue is null) throw new NullReferenceException();

        _osuUser.RenderSettings.BackgroundDim = double.Parse(settingValue, CultureInfo.InvariantCulture);
        await BackgroundDim();
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
        availableSkins = availableSkins.Skip((page - 1) * 7).Take(7).ToList();
        if (availableSkins.ToList().Count == 0)
        {
            await Context.Update.AnswerAsync(Context.BotClient);
            return;
        }

        string renderSkinNameNoOsk = _osuUser.RenderSettings.SkinName.EndsWith(".osk") ? _osuUser.RenderSettings.SkinName[..^4] : _osuUser.RenderSettings.SkinName;
        var availableSkinsAsIkm = availableSkins.Select(m => m.EndsWith(".osk") ? m[..^4] : m).Select(m => new InlineKeyboardButton[]
        {
            InlineKeyboardButton.WithCallbackData(renderSkinNameNoOsk == m ? Emojis.CheckMarkEmoji + m : m, $"rs ss {m}")
        });

        var ikm = new InlineKeyboardMarkup(
           [.. availableSkinsAsIkm,
            [InlineKeyboardButton.WithCallbackData("<", $"rs skin {page-1}"), InlineKeyboardButton.WithCallbackData($"{page}/{totalPages}", $"dummy"), InlineKeyboardButton.WithCallbackData(">", $"rs skin {page+1}")],
            [InlineKeyboardButton.WithCallbackData("Выбрать свой скин", $"rs set-custom-skin")],
            [InlineKeyboardButton.WithCallbackData("Назад", $"rs home")]
           ]
        );
        await Context.BotClient.EditMessageText(_chatId, Context.Update.Message!.Id, "Выбрать скин", replyMarkup: ikm);
    }

    async Task SetSkin(string? settingValue)
    {
        if (settingValue is null) throw new NullReferenceException();

        string[] splittedSettingValue = settingValue.Split(' ');
        _osuUser.RenderSettings.SkinName = settingValue + ".osk";

        int.TryParse(Context.Update.Message!.ReplyMarkup!.InlineKeyboard.First(m => m.Any(m => m.Text is "<")).First().CallbackData!.Split(' ').Last(), out int pageBefore);
        int currentPage = pageBefore + 1;
        await Skin(currentPage.ToString());
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

    async Task MotionBlur()
    {
        _osuUser.RenderSettings.MotionBlur = !_osuUser.RenderSettings.MotionBlur;
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

    async Task Leaderboard()
    {
        _osuUser.RenderSettings.Leaderboard = !_osuUser.RenderSettings.Leaderboard;
        await Home();
    }

    async Task StrainGraph()
    {
        _osuUser.RenderSettings.StrainGraph = !_osuUser.RenderSettings.StrainGraph;
        await Home();
    }
}