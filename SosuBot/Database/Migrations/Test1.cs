using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SosuBot.Migrations
{
    /// <inheritdoc />
    public partial class Test1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "OsuUsers",
                keyColumn: "TelegramId",
                keyValue: 0L,
                column: "IsAdmin",
                value: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "OsuUsers",
                keyColumn: "TelegramId",
                keyValue: 0L,
                column: "IsAdmin",
                value: false);
        }
    }
}
