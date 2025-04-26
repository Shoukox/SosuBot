using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SosuBot.Migrations
{
    /// <inheritdoc />
    public partial class AddOsuUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OsuUsers",
                columns: table => new
                {
                    TelegramId = table.Column<long>(type: "INTEGER", nullable: false),
                    OsuUserId = table.Column<long>(type: "INTEGER", nullable: false),
                    OsuUsername = table.Column<string>(type: "TEXT", nullable: false),
                    PPValue = table.Column<double>(type: "REAL", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OsuUsers", x => x.TelegramId);
                });

            migrationBuilder.CreateTable(
                name: "TelegramChats",
                columns: table => new
                {
                    ChatId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    ChatMembers = table.Column<string>(type: "TEXT", nullable: true),
                    LastBeatmapId = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelegramChats", x => x.ChatId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OsuUsers");

            migrationBuilder.DropTable(
                name: "TelegramChats");
        }
    }
}
