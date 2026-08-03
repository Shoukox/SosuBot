using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SosuBot.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddTrackedScoreDeliveryPipeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TrackedPlayerCheckpoints",
                columns: table => new
                {
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    Mode = table.Column<int>(type: "integer", nullable: true),
                    BestScoreIds = table.Column<List<long>>(type: "bigint[]", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrackedPlayerCheckpoints", x => x.PlayerId);
                });

            migrationBuilder.CreateTable(
                name: "TrackedScoreEvents",
                columns: table => new
                {
                    ScoreId = table.Column<long>(type: "bigint", nullable: false),
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    ScoreJson = table.Column<string>(type: "jsonb", nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DetectedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrackedScoreEvents", x => x.ScoreId);
                });

            migrationBuilder.CreateTable(
                name: "TrackedScoreDeliveries",
                columns: table => new
                {
                    ScoreId = table.Column<long>(type: "bigint", nullable: false),
                    ChatId = table.Column<long>(type: "bigint", nullable: false),
                    IsAdminRecipient = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_TrackedScoreDeliveries", x => new { x.ScoreId, x.ChatId });
                    table.ForeignKey(
                        name: "FK_TrackedScoreDeliveries_TrackedScoreEvents_ScoreId",
                        column: x => x.ScoreId,
                        principalTable: "TrackedScoreEvents",
                        principalColumn: "ScoreId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TrackedScoreDeliveries_Pending",
                table: "TrackedScoreDeliveries",
                columns: new[] { "AvailableAtUtc", "LockedUntilUtc" },
                filter: "\"SentAtUtc\" IS NULL AND \"CancelledAtUtc\" IS NULL AND \"FailedAtUtc\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TrackedScoreEvents_PlayerId_OccurredAtUtc",
                table: "TrackedScoreEvents",
                columns: new[] { "PlayerId", "OccurredAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TrackedPlayerCheckpoints");

            migrationBuilder.DropTable(
                name: "TrackedScoreDeliveries");

            migrationBuilder.DropTable(
                name: "TrackedScoreEvents");
        }
    }
}
