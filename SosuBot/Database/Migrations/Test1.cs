#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;

namespace SosuBot.Database.Migrations;

/// <inheritdoc />
public partial class Test1 : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.UpdateData(
            "OsuUsers",
            "TelegramId",
            0L,
            "IsAdmin",
            true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.UpdateData(
            "OsuUsers",
            "TelegramId",
            0L,
            "IsAdmin",
            false);
    }
}