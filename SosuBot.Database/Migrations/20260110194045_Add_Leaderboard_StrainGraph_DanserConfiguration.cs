using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SosuBot.Migrations
{
    /// <inheritdoc />
    public partial class Add_Leaderboard_StrainGraph_DanserConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "OsuUsers",
                keyColumn: "TelegramId",
                keyValue: 728384906L,
                column: "RenderSettings",
                value: "{\"VideoWidth\":1280,\"VideoHeight\":720,\"Encoder\":\"h264_nvenc\",\"SkinName\":\"default\",\"GeneralVolume\":0.5,\"MusicVolume\":0.5,\"SampleVolume\":0.5,\"HitErrorMeter\":false,\"AimErrorMeter\":false,\"HPBar\":true,\"ShowPP\":false,\"HitCounter\":false,\"IgnoreFailsInReplays\":false,\"Video\":false,\"Storyboard\":false,\"Mods\":false,\"KeyOverlay\":false,\"Combo\":false,\"Leaderboard\":false,\"StrainGraph\":true}");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "OsuUsers",
                keyColumn: "TelegramId",
                keyValue: 728384906L,
                column: "RenderSettings",
                value: "{\"Encoder\":\"h264_nvenc\",\"SkinName\":\"default\",\"GeneralVolume\":0.5,\"MusicVolume\":0.5,\"SampleVolume\":0.5,\"HitErrorMeter\":false,\"AimErrorMeter\":false,\"HPBar\":true,\"ShowPP\":false,\"HitCounter\":false,\"IgnoreFailsInReplays\":false,\"Video\":false,\"Storyboard\":false}");
        }
    }
}
