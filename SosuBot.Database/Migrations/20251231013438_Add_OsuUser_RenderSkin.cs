using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SosuBot.Migrations
{
    /// <inheritdoc />
    public partial class Add_OsuUser_RenderSkin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsGroup",
                table: "TelegramChats");

            migrationBuilder.AddColumn<string>(
                name: "RenderSkin",
                table: "OsuUsers",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "OsuUsers",
                keyColumn: "TelegramId",
                keyValue: 728384906L,
                column: "RenderSkin",
                value: "default");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RenderSkin",
                table: "OsuUsers");

            migrationBuilder.AddColumn<bool>(
                name: "IsGroup",
                table: "TelegramChats",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
