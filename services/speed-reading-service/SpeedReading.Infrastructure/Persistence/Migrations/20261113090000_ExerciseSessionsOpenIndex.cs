using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Decision 12.11.2026: the hourly clean-up closes exercise sessions that were left open. It reads the oldest
    /// open (1 Active, 2 Paused), non-assessment sessions in (start_time, id) order; this partial index holds exactly
    /// those rows, so the query does not scan the whole table. The index is small (open sessions only) and is built
    /// in the migration transaction. Down drops it.
    /// </summary>
    public partial class ExerciseSessionsOpenIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_exercise_sessions_open",
                schema: "speed_reading",
                table: "exercise_sessions",
                columns: new[] { "start_time", "id" },
                filter: "status IN (1, 2) AND assessment_attempt_id IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_exercise_sessions_open",
                schema: "speed_reading",
                table: "exercise_sessions");
        }
    }
}
