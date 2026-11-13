using SosuBot.TelegramHandlers.Commands;
using Xunit;

namespace SosuBot.PerformanceCalculator.Tests;

public sealed class BotCommandCatalogTests
{
    [Fact]
    public void PublicCommandsAndAliasesAreUnique()
    {
        string[] commands = BotCommandCatalog.Definitions
            .SelectMany(definition => definition.Aliases.Prepend(definition.Command))
            .ToArray();

        Assert.Equal(commands.Length, commands.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void DisablingAnAliasDisablesItsCanonicalCommand()
    {
        Assert.True(BotCommandCatalog.IsDisabled("/ll", ["/last"]));
        Assert.Equal("/last", BotCommandCatalog.Canonicalize("/ll"));
    }

    [Fact]
    public void CategoryToggleDisablesAndRestoresEveryCommand()
    {
        List<string> disabled = [];
        BotCommandDefinition[] scores = BotCommandCatalog.GetCategory(BotCommandCategory.Scores).ToArray();

        BotCommandCatalog.ToggleCategory(disabled, BotCommandCategory.Scores);
        Assert.Equal(scores.Length, disabled.Count);
        Assert.All(scores, command => Assert.Contains(command.Command, disabled));

        BotCommandCatalog.ToggleCategory(disabled, BotCommandCategory.Scores);
        Assert.Empty(disabled);
    }

    [Fact]
    public void IndividualToggleCanonicalizesAliasesAndCannotDisablePanel()
    {
        List<string> disabled = [];

        BotCommandCatalog.ToggleCommand(disabled, "/ll");
        Assert.Equal(["/last"], disabled);

        BotCommandCatalog.ToggleCommand(disabled, BotCommandCatalog.LimitsCommand);
        Assert.Equal(["/last"], disabled);
    }

    [Fact]
    public void ChatCommandsAreEphemeralOnlyWhenRestrictionsAreEnabled()
    {
        Telegram.Bot.Types.BotCommand[] defaults =
            BotCommandCatalog.CreateTelegramCommands([], ephemeral: false);
        Telegram.Bot.Types.BotCommand[] restricted =
            BotCommandCatalog.CreateTelegramCommands(["/user"], ephemeral: true);

        Assert.False(defaults.Single(command => command.Command == "user").IsEphemeral);
        Assert.True(defaults.Single(command => command.Command == "botlimits").IsEphemeral);
        Assert.DoesNotContain(restricted, command => command.Command == "user");
        Assert.All(restricted, command => Assert.True(command.IsEphemeral));
    }
}
