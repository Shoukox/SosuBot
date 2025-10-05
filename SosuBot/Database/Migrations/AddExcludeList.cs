#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;

namespace SosuBot.Database.Migrations;

/// <inheritdoc />
public partial class AddExcludeList : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            "ExcludeFromChatstats",
            "TelegramChats",
            "TEXT",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            "ExcludeFromChatstats",
            "TelegramChats");
    }
}