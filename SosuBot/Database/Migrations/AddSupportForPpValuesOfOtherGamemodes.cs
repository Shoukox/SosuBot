#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;

namespace SosuBot.Migrations;

/// <inheritdoc />
public partial class AddSupportForPpValuesOfOtherGamemodes : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameColumn(
            "PPValue",
            "OsuUsers",
            "TaikoPPValue");

        migrationBuilder.AddColumn<double>(
            "CatchPPValue",
            "OsuUsers",
            "REAL",
            nullable: true);

        migrationBuilder.AddColumn<double>(
            "ManiaPPValue",
            "OsuUsers",
            "REAL",
            nullable: true);

        migrationBuilder.AddColumn<double>(
            "StdPPValue",
            "OsuUsers",
            "REAL",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            "CatchPPValue",
            "OsuUsers");

        migrationBuilder.DropColumn(
            "ManiaPPValue",
            "OsuUsers");

        migrationBuilder.DropColumn(
            "StdPPValue",
            "OsuUsers");

        migrationBuilder.RenameColumn(
            "TaikoPPValue",
            "OsuUsers",
            "PPValue");
    }
}