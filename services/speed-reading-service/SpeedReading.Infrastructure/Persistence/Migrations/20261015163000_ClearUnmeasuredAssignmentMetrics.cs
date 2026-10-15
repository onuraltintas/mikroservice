using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace SpeedReading.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OwnedSpeedReadingDbContext))]
[Migration("20261015163000_ClearUnmeasuredAssignmentMetrics")]
public sealed class ClearUnmeasuredAssignmentMetrics : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        UPDATE speed_reading.student_assignments AS assignment
        SET "Score" = NULL, "KeyPerformanceMetric" = NULL
        FROM speed_reading.exercise_session_results AS result
        WHERE assignment."ResultId" = result.id
          AND assignment."StudentId" = result.student_id
          AND assignment."IsCompleted" = TRUE
          AND result.is_measured = FALSE
          AND (assignment."Score" IS NOT NULL OR assignment."KeyPerformanceMetric" IS NOT NULL);
        """);

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Missing measurements cannot be reconstructed as genuine zero scores.
    }
}
