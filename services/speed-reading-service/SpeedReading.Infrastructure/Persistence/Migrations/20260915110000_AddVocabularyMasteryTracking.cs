using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OwnedSpeedReadingDbContext))]
[Migration("20260915110000_AddVocabularyMasteryTracking")]
public partial class AddVocabularyMasteryTracking : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "has_been_mastered",
            schema: "speed_reading",
            table: "user_vocabulary_progress",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.Sql("""
            UPDATE speed_reading.user_vocabulary_progress
            SET has_been_mastered = TRUE
            WHERE box >= 5;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(
            name: "has_been_mastered",
            schema: "speed_reading",
            table: "user_vocabulary_progress");
}
