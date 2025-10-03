#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;

namespace SosuBot.Migrations;

/// <inheritdoc />
public partial class AddOsuMode : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            "OsuMode",
            "OsuUsers",
            "TEXT",
            nullable: false,
            defaultValue: "");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            "OsuMode",
            "OsuUsers");
    }
}