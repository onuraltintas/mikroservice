using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SpeedReading.Infrastructure.Persistence;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OwnedSpeedReadingDbContext))]
[Migration("20260828160000_AddAssessmentContentSnapshots")]
public partial class AddAssessmentContentSnapshots : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "content_snapshot_json",
            schema: "speed_reading",
            table: "assessment_attempt_exercises",
            type: "text",
            nullable: false,
            defaultValue: "{}");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "content_snapshot_json",
            schema: "speed_reading",
            table: "assessment_attempt_exercises");
    }
}
