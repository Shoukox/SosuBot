#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;

namespace SosuBot.Migrations;

/// <inheritdoc />
public partial class Test2 : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DeleteData(
            "OsuUsers",
            "TelegramId",
            0L);

        migrationBuilder.InsertData(
            "OsuUsers",
            new[]
            {
                "TelegramId", "CatchPPValue", "IsAdmin", "ManiaPPValue", "OsuMode", "OsuUserId", "OsuUsername",
                "StdPPValue", "TaikoPPValue"
            },
            new object[] { 728384906L, 0.0, true, 0.0, 0, 15319810L, "Shoukko", 0.0, 0.0 });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DeleteData(
            "OsuUsers",
            "TelegramId",
            728384906L);

        migrationBuilder.InsertData(
            "OsuUsers",
            new[]
            {
                "TelegramId", "CatchPPValue", "IsAdmin", "ManiaPPValue", "OsuMode", "OsuUserId", "OsuUsername",
                "StdPPValue", "TaikoPPValue"
            },
            new object[] { 0L, 0.0, true, 0.0, 0, 15319810L, "Shoukko", 0.0, 0.0 });
    }
}