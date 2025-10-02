using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SosuBot.Migrations
{
    /// <inheritdoc />
    public partial class Test2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "OsuUsers",
                keyColumn: "TelegramId",
                keyValue: 0L);

            migrationBuilder.InsertData(
                table: "OsuUsers",
                columns: new[] { "TelegramId", "CatchPPValue", "IsAdmin", "ManiaPPValue", "OsuMode", "OsuUserId", "OsuUsername", "StdPPValue", "TaikoPPValue" },
                values: new object[] { 728384906L, 0.0, true, 0.0, 0, 15319810L, "Shoukko", 0.0, 0.0 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "OsuUsers",
                keyColumn: "TelegramId",
                keyValue: 728384906L);

            migrationBuilder.InsertData(
                table: "OsuUsers",
                columns: new[] { "TelegramId", "CatchPPValue", "IsAdmin", "ManiaPPValue", "OsuMode", "OsuUserId", "OsuUsername", "StdPPValue", "TaikoPPValue" },
                values: new object[] { 0L, 0.0, true, 0.0, 0, 15319810L, "Shoukko", 0.0, 0.0 });
        }
    }
}
