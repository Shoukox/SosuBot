#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;

namespace SosuBot.Database.Migrations;

/// <inheritdoc />
public partial class AddOsuUsers : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            "OsuUsers",
            table => new
            {
                TelegramId = table.Column<long>("INTEGER", nullable: false),
                OsuUserId = table.Column<long>("INTEGER", nullable: false),
                OsuUsername = table.Column<string>("TEXT", nullable: false),
                PPValue = table.Column<double>("REAL", nullable: true)
            },
            constraints: table => { table.PrimaryKey("PK_OsuUsers", x => x.TelegramId); });

        migrationBuilder.CreateTable(
            "TelegramChats",
            table => new
            {
                ChatId = table.Column<ulong>("INTEGER", nullable: false),
                ChatMembers = table.Column<string>("TEXT", nullable: true),
                LastBeatmapId = table.Column<long>("INTEGER", nullable: true)
            },
            constraints: table => { table.PrimaryKey("PK_TelegramChats", x => x.ChatId); });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            "OsuUsers");

        migrationBuilder.DropTable(
            "TelegramChats");
    }
}