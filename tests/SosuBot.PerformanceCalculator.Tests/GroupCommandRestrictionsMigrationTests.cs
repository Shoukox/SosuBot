using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SosuBot.Database;
using Xunit;

namespace SosuBot.PerformanceCalculator.Tests;

public sealed class GroupCommandRestrictionsMigrationTests
{
    [Fact]
    public void MigrationAddsPerChatRestrictionSettingsWithDisabledDefaults()
    {
        DbContextOptions<BotContext> options = new DbContextOptionsBuilder<BotContext>()
            .UseNpgsql("Host=localhost;Database=sosubot_test;Username=test;Password=test")
            .Options;
        using BotContext database = new(options);

        string script = database.Database.GetService<IMigrator>().GenerateScript(
            "20261104211000_AddBlockedTopicIdsToTelegramChats",
            "20261113170000_AddGroupCommandRestrictions");

        Assert.Contains("\"CommandRestrictionsEnabled\" boolean NOT NULL DEFAULT FALSE", script);
        Assert.Contains("\"CommandCooldownSeconds\" integer NOT NULL DEFAULT 0", script);
        Assert.Contains("\"DisabledCommands\" text[]", script);
        Assert.Contains("\"IgnoreMedia\" boolean NOT NULL DEFAULT FALSE", script);
    }
}
