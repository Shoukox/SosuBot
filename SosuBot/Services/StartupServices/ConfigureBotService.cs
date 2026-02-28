using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SosuBot.TelegramHandlers;
using SosuBot.TelegramHandlers.Abstract;
using SosuBot.TelegramHandlers.Callbacks;
using SosuBot.TelegramHandlers.Commands;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace SosuBot.Services.StartupServices;

public class ConfigureBotService(IServiceProvider serviceProvider) : IHostedService
{
    private ITelegramBotClient _botClient = serviceProvider.GetRequiredService<ITelegramBotClient>();
    private ILogger<ConfigureBotService> _logger = serviceProvider.GetRequiredService<ILogger<ConfigureBotService>>();

    private static IEnumerable<BotCommand> botCommands = [
        new("/help", "Lists all bot commands"),
        new("/botlang", "Changes the bot language"),
    ];

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        //await _botClient.LogOut();
        //_logger.LogInformation("Successfully logged out");

        await _botClient.SetMyCommands(botCommands, cancellationToken: cancellationToken);
        _logger.LogInformation("Successfully set bot commands");

        // Register commands
        RegisterCommand<StartCommand>(StartCommand.Commands);
        RegisterCommand<HelpCommand>(HelpCommand.Commands);
        RegisterCommand<SetLanguageCommand>(SetLanguageCommand.Commands);
        RegisterCommand<OsuSetCommand>(OsuSetCommand.Commands);
        RegisterCommand<OsuModeCommand>(OsuModeCommand.Commands);
        RegisterCommand<OsuUserbestCommand>(OsuUserbestCommand.Commands);
        RegisterCommand<OsuChatstatsCommand>(OsuChatstatsCommand.Commands);
        RegisterCommand<OsuChatstatsExcludeCommand>(OsuChatstatsExcludeCommand.Commands);
        RegisterCommand<OsuChatstatsIncludeCommand>(OsuChatstatsIncludeCommand.Commands);
        RegisterCommand<OsuCompareCommand>(OsuCompareCommand.Commands);
        RegisterCommandWithParameters(OsuLastCommand.Commands, () => new OsuLastCommand());
        RegisterCommand<OsuLastWithCoverCommand>(OsuLastWithCoverCommand.Commands);
        RegisterCommand<OsuLastPassedCommand>(OsuLastPassedCommand.Commands);
        RegisterCommandWithParameters(OsuUserCommand.Commands, () => new OsuUserCommand());
        RegisterCommand<OsuUserIdCommand>(OsuUserIdCommand.Commands);
        RegisterCommand<OsuScoreCommand>(OsuScoreCommand.Commands);
        RegisterCommand<MsgCommand>(MsgCommand.Commands);
        RegisterCommand<DbCommand>(DbCommand.Commands);
        RegisterCommand<CustomCommand>(CustomCommand.Commands);
        RegisterCommand<DeleteCommand>(DeleteCommand.Commands);
        RegisterCommand<GetDailyStatisticsCommand>(GetDailyStatisticsCommand.Commands);
        RegisterCommand<GetRankingCommand>(GetRankingCommand.Commands);
        RegisterCommand<ReplayRenderCommand>(ReplayRenderCommand.Commands);
        RegisterCommand<RenderSkinSetCommand>(RenderSkinSetCommand.Commands);
        RegisterCommand<RenderSettingsCommand>(RenderSettingsCommand.Commands);
        RegisterCommand<TrackCommand>(TrackCommand.Commands);
        RegisterCommand<OsuChatBeatmapLeaderboardCommand>(OsuChatBeatmapLeaderboardCommand.Commands);
        RegisterCommand<OsuCalcCommand>(OsuCalcCommand.Commands);
        RegisterCommand<OsuCalcManiaCommand>(OsuCalcManiaCommand.Commands);
        RegisterCommand<OsuUpdateCommand>(OsuUpdateCommand.Commands);

        // Register callbacks
        RegisterCallback<OsuUserCallback>(OsuUserCallback.Command);
        RegisterCallback<OsuUserBestCallback>(OsuUserBestCallback.Command);
        RegisterCallback<OsuSongPreviewCallback>(OsuSongPreviewCallback.Command);
        RegisterCallback<RenderStatusCallback>(RenderStatusCallback.Command);
        RegisterCallback<RenderSettingsCallback>(RenderSettingsCallback.Command);
        RegisterCallback<SetLanguageCallback>(SetLanguageCallback.Command);
    }

    void RegisterCommand<T>(IEnumerable<string> commands) where T : CommandBase<Message>
    {
        foreach (var cmd in commands)
            UpdateHandler.Commands[cmd] = () => ActivatorUtilities.CreateInstance<T>(serviceProvider);
    }
    void RegisterCommandWithParameters(IEnumerable<string> commands, Func<CommandBase<Message>> factory)
    {
        foreach (var cmd in commands)
            UpdateHandler.Commands[cmd] = factory;
    }
    void RegisterCallback<T>(string callbackData) where T : CommandBase<CallbackQuery>
    {
        UpdateHandler.Callbacks[callbackData] = () => ActivatorUtilities.CreateInstance<T>(serviceProvider);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}