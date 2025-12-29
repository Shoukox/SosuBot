using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SosuBot.Migrations
{
    /// <inheritdoc />
    public partial class Add_DailyStatistics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyStatistics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DayOfStatistic = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CountryCode = table.Column<string>(type: "text", nullable: false),
                    BeatmapsPlayed = table.Column<List<int>>(type: "integer[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyStatistics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserEntity",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    UserJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserEntity", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "ScoreEntity",
                columns: table => new
                {
                    ScoreId = table.Column<long>(type: "bigint", nullable: false),
                    ScoreJson = table.Column<string>(type: "jsonb", nullable: false),
                    DailyStatisticsId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScoreEntity", x => x.ScoreId);
                    table.ForeignKey(
                        name: "FK_ScoreEntity_DailyStatistics_DailyStatisticsId",
                        column: x => x.DailyStatisticsId,
                        principalTable: "DailyStatistics",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "DailyStatisticsUserEntity",
                columns: table => new
                {
                    ActiveUsersUserId = table.Column<int>(type: "integer", nullable: false),
                    DailyStatisticsId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyStatisticsUserEntity", x => new { x.ActiveUsersUserId, x.DailyStatisticsId });
                    table.ForeignKey(
                        name: "FK_DailyStatisticsUserEntity_DailyStatistics_DailyStatisticsId",
                        column: x => x.DailyStatisticsId,
                        principalTable: "DailyStatistics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DailyStatisticsUserEntity_UserEntity_ActiveUsersUserId",
                        column: x => x.ActiveUsersUserId,
                        principalTable: "UserEntity",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DailyStatisticsUserEntity_DailyStatisticsId",
                table: "DailyStatisticsUserEntity",
                column: "DailyStatisticsId");

            migrationBuilder.CreateIndex(
                name: "IX_ScoreEntity_DailyStatisticsId",
                table: "ScoreEntity",
                column: "DailyStatisticsId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyStatisticsUserEntity");

            migrationBuilder.DropTable(
                name: "ScoreEntity");

            migrationBuilder.DropTable(
                name: "UserEntity");

            migrationBuilder.DropTable(
                name: "DailyStatistics");
        }
    }
}
