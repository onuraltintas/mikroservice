using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OwnedSpeedReadingDbContext))]
[Migration("20260909100000_AddSpeedReadingIntegrityIndexes")]
public partial class AddSpeedReadingIntegrityIndexes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // The legacy deployment may already contain duplicate rows from retries
        // that happened before the unique constraints existed. Keep the first
        // completed row and detach later rows so the migration remains
        // deployable without deleting historical data.
        migrationBuilder.Sql(
            """
            WITH duplicate_slots AS (
                SELECT id,
                       ROW_NUMBER() OVER (
                           PARTITION BY "StudentProgramProgressId", "WeekNumber", "DayNumber", "ExerciseId"
                           ORDER BY "CompletedDate", id
                       ) AS row_number
                FROM speed_reading.daily_exercise_logs
                WHERE session_id IS NOT NULL
            )
            UPDATE speed_reading.daily_exercise_logs AS logs
            SET session_id = NULL,
                updated_at = NOW(),
                updated_by = 'migration:20260909100000'
            FROM duplicate_slots
            WHERE logs.id = duplicate_slots.id
              AND duplicate_slots.row_number > 1;

            WITH duplicate_sessions AS (
                SELECT id,
                       ROW_NUMBER() OVER (
                           PARTITION BY assessment_attempt_id, exercise_id
                           ORDER BY start_time, id
                       ) AS row_number
                FROM speed_reading.exercise_sessions
                WHERE assessment_attempt_id IS NOT NULL
            )
            UPDATE speed_reading.exercise_session_results AS results
            SET assessment_attempt_id = NULL,
                updated_at = NOW(),
                updated_by = 'migration:20260909100000'
            FROM duplicate_sessions
            WHERE results.session_id = duplicate_sessions.id
              AND duplicate_sessions.row_number > 1;

            WITH duplicate_sessions AS (
                SELECT id,
                       ROW_NUMBER() OVER (
                           PARTITION BY assessment_attempt_id, exercise_id
                           ORDER BY start_time, id
                       ) AS row_number
                FROM speed_reading.exercise_sessions
                WHERE assessment_attempt_id IS NOT NULL
            )
            UPDATE speed_reading.exercise_sessions AS sessions
            SET assessment_attempt_id = NULL,
                status = 5,
                end_time = COALESCE(end_time, NOW()),
                updated_at = NOW(),
                updated_by = 'migration:20260909100000'
            FROM duplicate_sessions
            WHERE sessions.id = duplicate_sessions.id
              AND duplicate_sessions.row_number > 1;
            """);

        migrationBuilder.CreateIndex(
            name: "ux_daily_exercise_logs_progress_slot",
            schema: "speed_reading",
            table: "daily_exercise_logs",
            columns: new[] { "StudentProgramProgressId", "WeekNumber", "DayNumber", "ExerciseId" },
            unique: true,
            filter: "session_id IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "ux_exercise_sessions_assessment_exercise",
            schema: "speed_reading",
            table: "exercise_sessions",
            columns: new[] { "assessment_attempt_id", "exercise_id" },
            unique: true,
            filter: "assessment_attempt_id IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ux_exercise_sessions_assessment_exercise",
            schema: "speed_reading",
            table: "exercise_sessions");

        migrationBuilder.DropIndex(
            name: "ux_daily_exercise_logs_progress_slot",
            schema: "speed_reading",
            table: "daily_exercise_logs");
    }
}
