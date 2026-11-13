using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SosuBot.Database;

#nullable disable

namespace SosuBot.Database.Migrations;

[DbContext(typeof(BotContext))]
[Migration("20261113170000_AddGroupCommandRestrictions")]
public partial class AddGroupCommandRestrictions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "CommandRestrictionsEnabled",
            table: "TelegramChats",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<int>(
            name: "CommandCooldownSeconds",
            table: "TelegramChats",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<string[]>(
            name: "DisabledCommands",
            table: "TelegramChats",
            type: "text[]",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "IgnoreMedia",
            table: "TelegramChats",
            type: "boolean",
            nullable: false,
            defaultValue: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "CommandRestrictionsEnabled",
            table: "TelegramChats");

        migrationBuilder.DropColumn(
            name: "CommandCooldownSeconds",
            table: "TelegramChats");

        migrationBuilder.DropColumn(
            name: "DisabledCommands",
            table: "TelegramChats");

        migrationBuilder.DropColumn(
            name: "IgnoreMedia",
            table: "TelegramChats");
    }
}
