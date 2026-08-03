using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SosuBot.Database.Migrations
{
    /// <inheritdoc />
    public partial class CompleteScoresObserverSplit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "TrackedPlayerCheckpoints",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "DailyStatisticsReportDeliveries",
                columns: table => new
                {
                    DailyStatisticsId = table.Column<int>(type: "integer", nullable: false),
                    Mode = table.Column<int>(type: "integer", nullable: false),
                    ChatId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AvailableAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LockedUntilUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    SentAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelledAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyStatisticsReportDeliveries", x => new { x.DailyStatisticsId, x.Mode });
                    table.ForeignKey(
                        name: "FK_DailyStatisticsReportDeliveries_DailyStatistics_DailyStatis~",
                        column: x => x.DailyStatisticsId,
                        principalTable: "DailyStatistics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScoreFeedCheckpoints",
                columns: table => new
                {
                    Source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Cursor = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScoreFeedCheckpoints", x => x.Source);
                });

            migrationBuilder.CreateTable(
                name: "TrackedPlayerSubscriptions",
                columns: table => new
                {
                    ChatId = table.Column<long>(type: "bigint", nullable: false),
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrackedPlayerSubscriptions", x => new { x.ChatId, x.PlayerId });
                    table.ForeignKey(
                        name: "FK_TrackedPlayerSubscriptions_TelegramChats_ChatId",
                        column: x => x.ChatId,
                        principalTable: "TelegramChats",
                        principalColumn: "ChatId",
                        onDelete: ReferentialAction.Cascade);
                });

            // Preserve existing /track configuration and use migration time as the
            // cutover watermark. The observer will only enqueue initial scores that
            // happened after this timestamp.
            migrationBuilder.Sql(
                """
                INSERT INTO "TrackedPlayerSubscriptions" ("ChatId", "PlayerId", "StartedAtUtc")
                SELECT chat."ChatId", player."PlayerId", CURRENT_TIMESTAMP
                FROM "TelegramChats" AS chat
                CROSS JOIN LATERAL unnest(chat."TrackedPlayers") AS player("PlayerId")
                WHERE chat."TrackedPlayers" IS NOT NULL
                ON CONFLICT ("ChatId", "PlayerId") DO NOTHING;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_DailyStatisticsReportDeliveries_Pending",
                table: "DailyStatisticsReportDeliveries",
                columns: new[] { "AvailableAtUtc", "LockedUntilUtc" },
                filter: "\"SentAtUtc\" IS NULL AND \"CancelledAtUtc\" IS NULL AND \"FailedAtUtc\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TrackedPlayerSubscriptions_PlayerId",
                table: "TrackedPlayerSubscriptions",
                column: "PlayerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyStatisticsReportDeliveries");

            migrationBuilder.DropTable(
                name: "ScoreFeedCheckpoints");

            migrationBuilder.DropTable(
                name: "TrackedPlayerSubscriptions");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "TrackedPlayerCheckpoints");
        }
    }
}
