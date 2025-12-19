using System;
using System.Collections.Generic;
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
                name: "ScoreEntity",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ScoreJson = table.Column<string>(type: "jsonb", nullable: false),
                    DailyStatisticsId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScoreEntity", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScoreEntity_DailyStatistics_DailyStatisticsId",
                        column: x => x.DailyStatisticsId,
                        principalTable: "DailyStatistics",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "UserEntity",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserJson = table.Column<string>(type: "jsonb", nullable: false),
                    DailyStatisticsId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserEntity", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserEntity_DailyStatistics_DailyStatisticsId",
                        column: x => x.DailyStatisticsId,
                        principalTable: "DailyStatistics",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScoreEntity_DailyStatisticsId",
                table: "ScoreEntity",
                column: "DailyStatisticsId");

            migrationBuilder.CreateIndex(
                name: "IX_UserEntity_DailyStatisticsId",
                table: "UserEntity",
                column: "DailyStatisticsId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScoreEntity");

            migrationBuilder.DropTable(
                name: "UserEntity");

            migrationBuilder.DropTable(
                name: "DailyStatistics");
        }
    }
}
