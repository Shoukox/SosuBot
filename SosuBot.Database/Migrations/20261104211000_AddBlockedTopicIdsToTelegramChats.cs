using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SosuBot.Database;

#nullable disable

namespace SosuBot.Database.Migrations;

[DbContext(typeof(BotContext))]
[Migration("20261104211000_AddBlockedTopicIdsToTelegramChats")]
public partial class AddBlockedTopicIdsToTelegramChats : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int[]>(
            name: "BlockedTopicIds",
            table: "TelegramChats",
            type: "integer[]",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "BlockedTopicIds",
            table: "TelegramChats");
    }
}
