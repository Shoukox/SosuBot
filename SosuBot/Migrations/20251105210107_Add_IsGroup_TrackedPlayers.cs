using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SosuBot.Migrations
{
    /// <inheritdoc />
    public partial class Add_IsGroup_TrackedPlayers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsGroup",
                table: "TelegramChats",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<List<int>>(
                name: "TrackedPlayers",
                table: "TelegramChats",
                type: "integer[]",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsGroup",
                table: "TelegramChats");

            migrationBuilder.DropColumn(
                name: "TrackedPlayers",
                table: "TelegramChats");
        }
    }
}
