using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OwnedSpeedReadingDbContext))]
[Migration("20260827144000_AddOwnedCatalogWriteSupport")]
public partial class AddOwnedCatalogWriteSupport : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        AddSoftDeleteColumns(migrationBuilder, "exercise_type_categories");
        AddSoftDeleteColumns(migrationBuilder, "exercise_types");
        AddSoftDeleteColumns(migrationBuilder, "exercises");
        AddSoftDeleteColumns(migrationBuilder, "reading_texts");
        AddSoftDeleteColumns(migrationBuilder, "reading_questions");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        DropSoftDeleteColumns(migrationBuilder, "reading_questions");
        DropSoftDeleteColumns(migrationBuilder, "reading_texts");
        DropSoftDeleteColumns(migrationBuilder, "exercises");
        DropSoftDeleteColumns(migrationBuilder, "exercise_types");
        DropSoftDeleteColumns(migrationBuilder, "exercise_type_categories");
    }

    private static void AddSoftDeleteColumns(MigrationBuilder migrationBuilder, string table)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsDeleted",
            schema: "speed_reading",
            table: table,
            type: "boolean",
            nullable: false,
            defaultValue: false);
        migrationBuilder.AddColumn<DateTime>(
            name: "DeletedAt",
            schema: "speed_reading",
            table: table,
            type: "timestamp with time zone",
            nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "DeletedBy",
            schema: "speed_reading",
            table: table,
            type: "character varying(100)",
            maxLength: 100,
            nullable: true);
    }

    private static void DropSoftDeleteColumns(MigrationBuilder migrationBuilder, string table)
    {
        migrationBuilder.DropColumn(name: "IsDeleted", schema: "speed_reading", table: table);
        migrationBuilder.DropColumn(name: "DeletedAt", schema: "speed_reading", table: table);
        migrationBuilder.DropColumn(name: "DeletedBy", schema: "speed_reading", table: table);
    }
}
