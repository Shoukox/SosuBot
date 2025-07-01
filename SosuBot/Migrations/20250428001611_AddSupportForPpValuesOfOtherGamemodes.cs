using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SosuBot.Migrations
{
    /// <inheritdoc />
    public partial class AddSupportForPpValuesOfOtherGamemodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PPValue",
                table: "OsuUsers",
                newName: "TaikoPPValue");

            migrationBuilder.AddColumn<double>(
                name: "CatchPPValue",
                table: "OsuUsers",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "ManiaPPValue",
                table: "OsuUsers",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "StdPPValue",
                table: "OsuUsers",
                type: "REAL",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CatchPPValue",
                table: "OsuUsers");

            migrationBuilder.DropColumn(
                name: "ManiaPPValue",
                table: "OsuUsers");

            migrationBuilder.DropColumn(
                name: "StdPPValue",
                table: "OsuUsers");

            migrationBuilder.RenameColumn(
                name: "TaikoPPValue",
                table: "OsuUsers",
                newName: "PPValue");
        }
    }
}
