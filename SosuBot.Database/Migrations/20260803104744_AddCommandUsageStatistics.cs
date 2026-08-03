using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SosuBot.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddCommandUsageStatistics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CommandUsageAggregates",
                columns: table => new
                {
                    Command = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    BucketStartUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SuccessCount = table.Column<long>(type: "bigint", nullable: false),
                    ErrorCount = table.Column<long>(type: "bigint", nullable: false),
                    CancelledCount = table.Column<long>(type: "bigint", nullable: false),
                    TotalDurationMilliseconds = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommandUsageAggregates", x => new { x.Command, x.BucketStartUtc });
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommandUsageAggregates_BucketStartUtc",
                table: "CommandUsageAggregates",
                column: "BucketStartUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommandUsageAggregates");
        }
    }
}
