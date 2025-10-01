using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SosuBot.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAdmin",
                table: "OsuUsers",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.InsertData(
                table: "OsuUsers",
                columns: new[] { "TelegramId", "CatchPPValue", "IsAdmin", "ManiaPPValue", "OsuMode", "OsuUserId", "OsuUsername", "StdPPValue", "TaikoPPValue" },
                values: new object[] { 0L, 0.0, false, 0.0, 0, 15319810L, "Shoukko", 0.0, 0.0 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "OsuUsers",
                keyColumn: "TelegramId",
                keyValue: 0L);

            migrationBuilder.DropColumn(
                name: "IsAdmin",
                table: "OsuUsers");
        }
    }
}
