using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using SosuBot.Helpers.Types;

#nullable disable

namespace SosuBot.Migrations
{
    /// <inheritdoc />
    public partial class Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:playmode", "catch,mania,osu,taiko");

            migrationBuilder.CreateTable(
                name: "OsuUsers",
                columns: table => new
                {
                    TelegramId = table.Column<long>(type: "bigint", nullable: false),
                    OsuUserId = table.Column<long>(type: "bigint", nullable: false),
                    OsuUsername = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    OsuMode = table.Column<Playmode>(type: "playmode", nullable: false),
                    StdPPValue = table.Column<double>(type: "double precision", nullable: false),
                    TaikoPPValue = table.Column<double>(type: "double precision", nullable: false),
                    CatchPPValue = table.Column<double>(type: "double precision", nullable: false),
                    ManiaPPValue = table.Column<double>(type: "double precision", nullable: false),
                    IsAdmin = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OsuUsers", x => x.TelegramId);
                });

            migrationBuilder.CreateTable(
                name: "TelegramChats",
                columns: table => new
                {
                    ChatId = table.Column<long>(type: "bigint", nullable: false),
                    ChatMembers = table.Column<List<long>>(type: "bigint[]", nullable: true),
                    ExcludeFromChatstats = table.Column<List<long>>(type: "bigint[]", nullable: true),
                    LastBeatmapId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelegramChats", x => x.ChatId);
                });

            migrationBuilder.InsertData(
                table: "OsuUsers",
                columns: new[] { "TelegramId", "CatchPPValue", "IsAdmin", "ManiaPPValue", "OsuMode", "OsuUserId", "OsuUsername", "StdPPValue", "TaikoPPValue" },
                values: new object[] { 728384906L, 0.0, true, 0.0, Playmode.Osu, 15319810L, "Shoukko", 0.0, 0.0 });
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
