using SosuBot.Localization;
using Telegram.Bot.Types;

namespace SosuBot.TelegramHandlers.Commands;

public enum BotCommandCategory
{
    Player,
    Scores,
    ChatStatistics,
    Graphics,
    Utilities,
    Management
}

public sealed record BotCommandDefinition(
    string Command,
    string Description,
    BotCommandCategory Category,
    string[] Aliases,
    bool IsManagement = false);

public static class BotCommandCatalog
{
    public const string LimitsCommand = "/botlimits";

    public static readonly BotCommandCategory[] Categories =
    [
        BotCommandCategory.Player,
        BotCommandCategory.Scores,
        BotCommandCategory.ChatStatistics,
        BotCommandCategory.Graphics,
        BotCommandCategory.Utilities,
        BotCommandCategory.Management
    ];

    public static readonly IReadOnlyList<BotCommandDefinition> Definitions =
    [
        new(OsuSetCommand.Commands[0], OsuSetCommand.Description, BotCommandCategory.Player, []),
        new(OsuModeCommand.Commands[0], OsuModeCommand.Description, BotCommandCategory.Player, []),
        new(OsuUserCommand.Commands[0], OsuUserCommand.Description, BotCommandCategory.Player, OsuUserCommand.Commands[1..]),
        new(OsuUserWidgetCommand.Commands[0], OsuUserWidgetCommand.Description, BotCommandCategory.Player, []),
        new(OsuUserIdCommand.Commands[0], OsuUserIdCommand.Description, BotCommandCategory.Player, OsuUserIdCommand.Commands[1..]),
        new(OsuUpdateCommand.Commands[0], OsuUpdateCommand.Description, BotCommandCategory.Player, OsuUpdateCommand.Commands[1..]),
        new(OsuLastWithCoverCommand.Commands[0], OsuLastWithCoverCommand.Description, BotCommandCategory.Scores,
            [.. OsuLastWithCoverCommand.Commands[1..], .. OsuLastCommand.Commands]),
        new(OsuLastRichCommand.Commands[0], OsuLastRichCommand.Description, BotCommandCategory.Scores, []),
        new(OsuLastPassedCommand.Commands[0], OsuLastPassedCommand.Description, BotCommandCategory.Scores, OsuLastPassedCommand.Commands[1..]),
        new(OsuLastBestCommand.Commands[0], OsuLastBestCommand.Description, BotCommandCategory.Scores, OsuLastBestCommand.Commands[1..]),
        new(OsuScoreCommand.Commands[0], OsuScoreCommand.Description, BotCommandCategory.Scores, OsuScoreCommand.Commands[1..]),
        new(OsuUserbestCommand.Commands[0], OsuUserbestCommand.Description, BotCommandCategory.Scores, OsuUserbestCommand.Commands[1..]),
        new(OsuCompareCommand.Commands[0], OsuCompareCommand.Description, BotCommandCategory.Scores, OsuCompareCommand.Commands[1..]),
        new(OsuCalcCommand.Commands[0], OsuCalcCommand.Description, BotCommandCategory.Scores, []),
        new(GetRankingCommand.Commands[0], GetRankingCommand.Description, BotCommandCategory.Scores, []),
        new(GetDailyStatisticsCommand.Commands[0], GetDailyStatisticsCommand.Description, BotCommandCategory.Scores, GetDailyStatisticsCommand.Commands[1..]),
        new(GetWeeklyStatisticsCommand.Commands[0], GetWeeklyStatisticsCommand.Description, BotCommandCategory.Scores, []),
        new(GetMonthlyStatisticsCommand.Commands[0], GetMonthlyStatisticsCommand.Description, BotCommandCategory.Scores, []),
        new(OsuChatstatsCommand.Commands[0], OsuChatstatsCommand.Description, BotCommandCategory.ChatStatistics, OsuChatstatsCommand.Commands[1..]),
        new(OsuChatstatsExcludeCommand.Commands[0], OsuChatstatsExcludeCommand.Description, BotCommandCategory.ChatStatistics, []),
        new(OsuChatstatsIncludeCommand.Commands[0], OsuChatstatsIncludeCommand.Description, BotCommandCategory.ChatStatistics, []),
        new(OsuChatBeatmapLeaderboardCommand.Commands[0], OsuChatBeatmapLeaderboardCommand.Description, BotCommandCategory.ChatStatistics, OsuChatBeatmapLeaderboardCommand.Commands[1..]),
        new(OsuCardCommand.Commands[0], OsuCardCommand.Description, BotCommandCategory.Graphics, []),
        new(VideoPreviewCommand.Commands[0], VideoPreviewCommand.Description, BotCommandCategory.Graphics, []),
        new(PostVideoCommand.Commands[0], PostVideoCommand.Description, BotCommandCategory.Graphics, []),
        new(ReplayRenderCommand.Commands[0], ReplayRenderCommand.Description, BotCommandCategory.Graphics, []),
        new(RenderSettingsCommand.Commands[0], RenderSettingsCommand.Description, BotCommandCategory.Graphics, []),
        new(RenderSkinSetCommand.Commands[0], RenderSkinSetCommand.Description, BotCommandCategory.Graphics, []),
        new(GetSkinCommand.Commands[0], GetSkinCommand.Description, BotCommandCategory.Graphics, []),
        new(RenderCursorSizeCommand.Commands[0], RenderCursorSizeCommand.Description, BotCommandCategory.Graphics, []),
        new(RenderScrollSpeedCommand.Commands[0], RenderScrollSpeedCommand.Description, BotCommandCategory.Graphics, []),
        new(RandomBeatmapCommand.Commands[0], RandomBeatmapCommand.Description, BotCommandCategory.Utilities, []),
        new(TrackCommand.Commands[0], TrackCommand.Description, BotCommandCategory.Utilities, []),
        new(TopicBotCommand.Commands[0], TopicBotCommand.Description, BotCommandCategory.Management, []),
        new(SetLanguageCommand.Commands[0], SetLanguageCommand.Description, BotCommandCategory.Management, []),
        new(HelpCommand.Commands[0], HelpCommand.Description, BotCommandCategory.Management, []),
        new(StartCommand.Commands[0], StartCommand.Description, BotCommandCategory.Management, []),
        new(LimitsCommand, "панель ограничений группы", BotCommandCategory.Management, [], IsManagement: true)
    ];

    private static readonly IReadOnlyDictionary<string, BotCommandDefinition> DefinitionsByCommand =
        Definitions
            .SelectMany(definition => definition.Aliases.Prepend(definition.Command)
                .Select(alias => (Command: alias, Definition: definition)))
            .ToDictionary(item => item.Command, item => item.Definition, StringComparer.OrdinalIgnoreCase);

    public static string Canonicalize(string command) =>
        DefinitionsByCommand.TryGetValue(command, out BotCommandDefinition? definition)
            ? definition.Command
            : command;

    public static bool IsDisabled(string command, IEnumerable<string>? disabledCommands)
    {
        string canonicalCommand = Canonicalize(command);
        return disabledCommands?.Contains(canonicalCommand, StringComparer.OrdinalIgnoreCase) == true;
    }

    public static IReadOnlyList<BotCommandDefinition> GetCategory(BotCommandCategory category) =>
        Definitions.Where(definition => !definition.IsManagement && definition.Category == category).ToArray();

    public static BotCommandCategory? GetCategoryForCommand(string command) =>
        DefinitionsByCommand.TryGetValue(command, out BotCommandDefinition? definition)
            ? definition.Category
            : null;

    public static void ToggleCategory(List<string> disabledCommands, BotCommandCategory category)
    {
        string[] commands = GetCategory(category)
            .Select(definition => definition.Command)
            .ToArray();
        bool enable = commands.All(command =>
            disabledCommands.Contains(command, StringComparer.OrdinalIgnoreCase));

        foreach (string command in commands)
        {
            disabledCommands.RemoveAll(value => value.Equals(command, StringComparison.OrdinalIgnoreCase));
            if (!enable)
                disabledCommands.Add(command);
        }
    }

    public static void ToggleCommand(List<string> disabledCommands, string command)
    {
        if (DefinitionsByCommand.TryGetValue(command, out BotCommandDefinition? definition) &&
            !definition.IsManagement)
        {
            int removed = disabledCommands.RemoveAll(value =>
                value.Equals(definition.Command, StringComparison.OrdinalIgnoreCase));
            if (removed == 0)
                disabledCommands.Add(definition.Command);
        }
    }

    public static BotCommand[] CreateTelegramCommands(IEnumerable<string>? disabledCommands, bool ephemeral)
    {
        HashSet<string> disabled = new(disabledCommands ?? [], StringComparer.OrdinalIgnoreCase);

        return Definitions
            .Where(definition => definition.IsManagement ||
                !disabled.Contains(definition.Command))
            .Select(definition => new BotCommand(
                definition.Command.TrimStart('/'),
                definition.Description)
            {
                IsEphemeral = ephemeral || definition.IsManagement
            })
            .ToArray();
    }

    public static string GetCategoryName(BotCommandCategory category, ILocalization language) =>
        category switch
        {
            BotCommandCategory.Player => language.groupLimits_categoryPlayer,
            BotCommandCategory.Scores => language.groupLimits_categoryScores,
            BotCommandCategory.ChatStatistics => language.groupLimits_categoryChatStatistics,
            BotCommandCategory.Graphics => language.groupLimits_categoryGraphics,
            BotCommandCategory.Utilities => language.groupLimits_categoryUtilities,
            BotCommandCategory.Management => language.groupLimits_categoryManagement,
            _ => throw new ArgumentOutOfRangeException(nameof(category), category, null)
        };
}
