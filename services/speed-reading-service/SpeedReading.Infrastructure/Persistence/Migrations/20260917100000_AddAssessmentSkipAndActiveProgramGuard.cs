using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OwnedSpeedReadingDbContext))]
[Migration("20260917100000_AddAssessmentSkipAndActiveProgramGuard")]
public partial class AddAssessmentSkipAndActiveProgramGuard : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "is_skipped",
            schema: "speed_reading",
            table: "assessment_attempts",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        // Keep the most recently assigned active program when older data has
        // more than one active row for the same student. This makes the new
        // uniqueness guard safe to apply without deleting progress or logs.
        migrationBuilder.Sql(
            """
            UPDATE speed_reading.student_program_progress AS older
            SET "IsActive" = FALSE,
                updated_at = NOW()
            WHERE older."IsActive" = TRUE
              AND older."CompletedDate" IS NULL
              AND EXISTS (
                  SELECT 1
                  FROM speed_reading.student_program_progress AS newer
                  WHERE newer."UserId" = older."UserId"
                    AND newer."IsActive" = TRUE
                    AND newer."CompletedDate" IS NULL
                    AND (
                        newer."AssignedDate" > older."AssignedDate"
                        OR (
                            newer."AssignedDate" = older."AssignedDate"
                            AND newer.id > older.id
                        )
                    )
              );
            """);

        migrationBuilder.CreateIndex(
            name: "ux_student_program_progress_active_user",
            schema: "speed_reading",
            table: "student_program_progress",
            column: "UserId",
            unique: true,
            filter: "\"IsActive\" = TRUE AND \"CompletedDate\" IS NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ux_student_program_progress_active_user",
            schema: "speed_reading",
            table: "student_program_progress");

        migrationBuilder.DropColumn(
            name: "is_skipped",
            schema: "speed_reading",
            table: "assessment_attempts");
    }
}
