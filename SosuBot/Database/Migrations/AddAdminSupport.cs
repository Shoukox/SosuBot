#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;

namespace SosuBot.Database.Migrations;

/// <inheritdoc />
public partial class AddAdminSupport : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            "IsAdmin",
            "OsuUsers",
            "INTEGER",
            nullable: false,
            defaultValue: false);

        migrationBuilder.InsertData(
            "OsuUsers",
            new[]
            {
                "TelegramId", "CatchPPValue", "IsAdmin", "ManiaPPValue", "OsuMode", "OsuUserId", "OsuUsername",
                "StdPPValue", "TaikoPPValue"
            },
            new object[] { 0L, 0.0, false, 0.0, 0, 15319810L, "Shoukko", 0.0, 0.0 });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DeleteData(
            "OsuUsers",
            "TelegramId",
            0L);

        migrationBuilder.DropColumn(
            "IsAdmin",
            "OsuUsers");
    }
}