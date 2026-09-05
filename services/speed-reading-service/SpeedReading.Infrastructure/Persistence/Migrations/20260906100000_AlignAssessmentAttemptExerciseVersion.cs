using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OwnedSpeedReadingDbContext))]
[Migration("20260906100000_AlignAssessmentAttemptExerciseVersion")]
public partial class AlignAssessmentAttemptExerciseVersion : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // AssessmentAttemptExercise is an Entity, not an AggregateRoot, so the
        // runtime model does not send this legacy column on insert. Keep the
        // column for existing databases but let PostgreSQL supply its value.
        migrationBuilder.AlterColumn<int>(
            name: "version",
            schema: "speed_reading",
            table: "assessment_attempt_exercises",
            type: "integer",
            nullable: false,
            defaultValue: 0,
            oldClrType: typeof(int),
            oldType: "integer");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<int>(
            name: "version",
            schema: "speed_reading",
            table: "assessment_attempt_exercises",
            type: "integer",
            nullable: false,
            oldClrType: typeof(int),
            oldType: "integer",
            oldDefaultValue: 0);
    }
}
