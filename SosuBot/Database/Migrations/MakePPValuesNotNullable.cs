#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;

// ReSharper disable InconsistentNaming

namespace SosuBot.Database.Migrations;

/// <inheritdoc />
public partial class MakePPValuesNotNullable : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<double>(
            "TaikoPPValue",
            "OsuUsers",
            "REAL",
            nullable: false,
            defaultValue: 0.0,
            oldClrType: typeof(double),
            oldType: "REAL",
            oldNullable: true);

        migrationBuilder.AlterColumn<double>(
            "StdPPValue",
            "OsuUsers",
            "REAL",
            nullable: false,
            defaultValue: 0.0,
            oldClrType: typeof(double),
            oldType: "REAL",
            oldNullable: true);

        migrationBuilder.AlterColumn<int>(
            "OsuMode",
            "OsuUsers",
            "INTEGER",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "TEXT");

        migrationBuilder.AlterColumn<double>(
            "ManiaPPValue",
            "OsuUsers",
            "REAL",
            nullable: false,
            defaultValue: 0.0,
            oldClrType: typeof(double),
            oldType: "REAL",
            oldNullable: true);

        migrationBuilder.AlterColumn<double>(
            "CatchPPValue",
            "OsuUsers",
            "REAL",
            nullable: false,
            defaultValue: 0.0,
            oldClrType: typeof(double),
            oldType: "REAL",
            oldNullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<double>(
            "TaikoPPValue",
            "OsuUsers",
            "REAL",
            nullable: true,
            oldClrType: typeof(double),
            oldType: "REAL");

        migrationBuilder.AlterColumn<double>(
            "StdPPValue",
            "OsuUsers",
            "REAL",
            nullable: true,
            oldClrType: typeof(double),
            oldType: "REAL");

        migrationBuilder.AlterColumn<string>(
            "OsuMode",
            "OsuUsers",
            "TEXT",
            nullable: false,
            oldClrType: typeof(int),
            oldType: "INTEGER");

        migrationBuilder.AlterColumn<double>(
            "ManiaPPValue",
            "OsuUsers",
            "REAL",
            nullable: true,
            oldClrType: typeof(double),
            oldType: "REAL");

        migrationBuilder.AlterColumn<double>(
            "CatchPPValue",
            "OsuUsers",
            "REAL",
            nullable: true,
            oldClrType: typeof(double),
            oldType: "REAL");
    }
}