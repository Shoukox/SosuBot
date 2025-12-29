using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SosuBot.Migrations
{
    /// <inheritdoc />
    public partial class Add_RenderSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RenderSettings",
                table: "OsuUsers",
                type: "jsonb",
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "OsuUsers",
                keyColumn: "TelegramId",
                keyValue: 728384906L,
                column: "RenderSettings",
                value: "{\"Encoder\":\"h264_nvenc\",\"SkinName\":\"default\",\"GeneralVolume\":0.5,\"MusicVolume\":0.5,\"SampleVolume\":0.5,\"HitErrorMeter\":false,\"AimErrorMeter\":false,\"HPBar\":true,\"ShowPP\":false,\"HitCounter\":false,\"IgnoreFailsInReplays\":false,\"Video\":false,\"Storyboard\":false}");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RenderSettings",
                table: "OsuUsers");
        }
    }
}
