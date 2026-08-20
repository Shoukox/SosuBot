using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SosuBot.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddOsuUserProfileStatisticsSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "CatchAccuracyValue",
                table: "OsuUsers",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CatchPlayTimeValue",
                table: "OsuUsers",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "ManiaAccuracyValue",
                table: "OsuUsers",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ManiaPlayTimeValue",
                table: "OsuUsers",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "StdAccuracyValue",
                table: "OsuUsers",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "StdPlayTimeValue",
                table: "OsuUsers",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "TaikoAccuracyValue",
                table: "OsuUsers",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "TaikoPlayTimeValue",
                table: "OsuUsers",
                type: "bigint",
                nullable: true);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CatchAccuracyValue",
                table: "OsuUsers");

            migrationBuilder.DropColumn(
                name: "CatchPlayTimeValue",
                table: "OsuUsers");

            migrationBuilder.DropColumn(
                name: "ManiaAccuracyValue",
                table: "OsuUsers");

            migrationBuilder.DropColumn(
                name: "ManiaPlayTimeValue",
                table: "OsuUsers");

            migrationBuilder.DropColumn(
                name: "StdAccuracyValue",
                table: "OsuUsers");

            migrationBuilder.DropColumn(
                name: "StdPlayTimeValue",
                table: "OsuUsers");

            migrationBuilder.DropColumn(
                name: "TaikoAccuracyValue",
                table: "OsuUsers");

            migrationBuilder.DropColumn(
                name: "TaikoPlayTimeValue",
                table: "OsuUsers");

        }
    }
}
