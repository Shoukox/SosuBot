using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using SosuBot.Configuration;
using SosuBot.TelegramHandlers;
using SosuBot.TelegramHandlers.Abstract;
using SosuBot.TelegramHandlers.Callbacks;
using SosuBot.TelegramHandlers.Commands;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace SosuBot.Services.StartupServices;

public class ConfigureBotService(IServiceProvider serviceProvider) : IHostedService
{
    private readonly ITelegramBotClient _botClient = serviceProvider.GetRequiredService<ITelegramBotClient>();
    private readonly ILogger<ConfigureBotService> _logger = serviceProvider.GetRequiredService<ILogger<ConfigureBotService>>();
    private readonly BotConfiguration _botConfig = serviceProvider.GetRequiredService<IOptions<BotConfiguration>>().Value;
    private readonly BeatmapFileCache _beatmapFileCache = serviceProvider.GetRequiredService<BeatmapFileCache>();

    private static readonly BotCommand[] BotCommands = [
        CreateBotCommand(OsuSetCommand.Commands[0], OsuSetCommand.Description),
        CreateBotCommand(OsuModeCommand.Commands[0], OsuModeCommand.Description),
        CreateBotCommand(OsuUserCommand.Commands[0], OsuUserCommand.Description),
        CreateBotCommand(OsuUserIdCommand.Commands[0], OsuUserIdCommand.Description),
        CreateBotCommand(OsuLastWithCoverCommand.Commands[0], OsuLastWithCoverCommand.Description),
        CreateBotCommand(OsuLastPassedCommand.Commands[0], OsuLastPassedCommand.Description),
        CreateBotCommand(OsuScoreCommand.Commands[0], OsuScoreCommand.Description),
        CreateBotCommand(OsuUserbestCommand.Commands[0], OsuUserbestCommand.Description),
        CreateBotCommand(OsuCompareCommand.Commands[0], OsuCompareCommand.Description),
        CreateBotCommand(OsuChatstatsCommand.Commands[0], OsuChatstatsCommand.Description),
        CreateBotCommand(OsuChatstatsExcludeCommand.Commands[0], OsuChatstatsExcludeCommand.Description),
        CreateBotCommand(OsuChatstatsIncludeCommand.Commands[0], OsuChatstatsIncludeCommand.Description),
        CreateBotCommand(OsuLastBestCommand.Commands[0], OsuLastBestCommand.Description),
        CreateBotCommand(OsuUpdateCommand.Commands[0], OsuUpdateCommand.Description),
        CreateBotCommand(OsuChatBeatmapLeaderboardCommand.Commands[0], OsuChatBeatmapLeaderboardCommand.Description),
        CreateBotCommand(OsuCardCommand.Commands[0], OsuCardCommand.Description),
        CreateBotCommand(RandomBeatmapCommand.Commands[0], RandomBeatmapCommand.Description),
        CreateBotCommand(OsuCalcCommand.Commands[0], OsuCalcCommand.Description),
        CreateBotCommand(OsuCalcManiaCommand.Commands[0], OsuCalcManiaCommand.Description),
        CreateBotCommand(TrackCommand.Commands[0], TrackCommand.Description),
        CreateBotCommand(ReplayRenderCommand.Commands[0], ReplayRenderCommand.Description),
        CreateBotCommand(RenderSettingsCommand.Commands[0], RenderSettingsCommand.Description),
        CreateBotCommand(RenderSkinSetCommand.Commands[0], RenderSkinSetCommand.Description),
        CreateBotCommand(RenderCursorSizeCommand.Commands[0], RenderCursorSizeCommand.Description),
        CreateBotCommand(RenderScrollSpeedCommand.Commands[0], RenderScrollSpeedCommand.Description),
        CreateBotCommand(VideoPreviewCommand.Commands[0], VideoPreviewCommand.Description),
        CreateBotCommand(GetRankingCommand.Commands[0], GetRankingCommand.Description),
        CreateBotCommand(GetDailyStatisticsCommand.Commands[0], GetDailyStatisticsCommand.Description),
        CreateBotCommand(HelpCommand.Commands[0], HelpCommand.Description),
        CreateBotCommand(SetLanguageCommand.Commands[0], SetLanguageCommand.Description),
        CreateBotCommand(StartCommand.Commands[0], StartCommand.Description),
    ];

    private static BotCommand CreateBotCommand(string command, string description) =>
        new(command.TrimStart('/'), description);

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _beatmapFileCache.WarmUpAsync().WaitAsync(cancellationToken);

        //await _botClient.LogOut();
        //_logger.LogInformation("Successfully logged out");

        // Configure bot
        User botUser = await _botClient.GetMe();
        _botConfig.Username = botUser.Username!;
        _botConfig.Id = botUser.Id;

        // Configure bot commands
        await _botClient.SetMyCommands(BotCommands, cancellationToken: cancellationToken);
        _logger.LogInformation("Successfully set bot commands");

        // Register command handlers
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
        RegisterCommand<OsuLastBestCommand>(OsuLastBestCommand.Commands);
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
        RegisterCommand<RenderCursorSizeCommand>(RenderCursorSizeCommand.Commands);
        RegisterCommand<RenderScrollSpeedCommand>(RenderScrollSpeedCommand.Commands);
        RegisterCommand<RenderSettingsCommand>(RenderSettingsCommand.Commands);
        RegisterCommand<TrackCommand>(TrackCommand.Commands);
        RegisterCommand<OsuChatBeatmapLeaderboardCommand>(OsuChatBeatmapLeaderboardCommand.Commands);
        RegisterCommand<OsuCalcCommand>(OsuCalcCommand.Commands);
        RegisterCommand<OsuCalcManiaCommand>(OsuCalcManiaCommand.Commands);
        RegisterCommand<OsuUpdateCommand>(OsuUpdateCommand.Commands);
        RegisterCommand<RandomBeatmapCommand>(RandomBeatmapCommand.Commands);
        RegisterCommand<OsuCardCommand>(OsuCardCommand.Commands);
        RegisterCommand<VideoPreviewCommand>(VideoPreviewCommand.Commands);
        RegisterCommand<PostVideoCommand>(PostVideoCommand.Commands);

        // Register callbacks
        RegisterCallback<OsuUserCallback>(OsuUserCallback.Command);
        RegisterCallback<OsuUserBestCallback>(OsuUserBestCallback.Command);
        RegisterCallback<OsuSongPreviewCallback>(OsuSongPreviewCallback.Command);
        RegisterCallback<RenderStatusCallback>(RenderStatusCallback.Command);
        RegisterCallback<RenderCancelCallback>(RenderCancelCallback.Command);
        RegisterCallback<RenderSettingsCallback>(RenderSettingsCallback.Command);
        RegisterCallback<SetLanguageCallback>(SetLanguageCallback.Command);
    }

    void RegisterCommand<T>(IEnumerable<string> commands) where T : CommandBase<Message>
    {
        string[] commandNames = commands.ToArray();
        string metricName = commandNames.First();
        foreach (var cmd in commandNames)
        {
            UpdateHandler.Commands[cmd] = () => ActivatorUtilities.CreateInstance<T>(serviceProvider);
            UpdateHandler.CommandMetricNames[cmd] = metricName;
        }
    }
    void RegisterCommandWithParameters(IEnumerable<string> commands, Func<CommandBase<Message>> factory)
    {
        string[] commandNames = commands.ToArray();
        string metricName = commandNames.First();
        foreach (var cmd in commandNames)
        {
            UpdateHandler.Commands[cmd] = factory;
            UpdateHandler.CommandMetricNames[cmd] = metricName;
        }
    }
    void RegisterCallback<T>(string callbackData) where T : CommandBase<CallbackQuery>
    {
        UpdateHandler.Callbacks[callbackData] = () => ActivatorUtilities.CreateInstance<T>(serviceProvider);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _beatmapFileCache.FlushIndexAsync(cancellationToken);
        _logger.LogInformation("Bot is stopping...");
    }
}
