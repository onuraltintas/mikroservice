using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyTaskSlotOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_daily_exercise_logs_progress_slot",
                schema: "speed_reading",
                table: "daily_exercise_logs");

            migrationBuilder.AddColumn<int>(
                name: "slot_order",
                schema: "speed_reading",
                table: "daily_exercise_logs",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_daily_exercise_logs_legacy_slot",
                schema: "speed_reading",
                table: "daily_exercise_logs",
                columns: new[] { "StudentProgramProgressId", "WeekNumber", "DayNumber", "ExerciseId" },
                unique: true,
                filter: "session_id IS NOT NULL AND slot_order IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_daily_exercise_logs_progress_slot",
                schema: "speed_reading",
                table: "daily_exercise_logs",
                columns: new[] { "StudentProgramProgressId", "WeekNumber", "DayNumber", "slot_order" },
                unique: true,
                filter: "slot_order IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Do not erase slot identity if repeated tasks have already produced results.
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM speed_reading.daily_exercise_logs
                    WHERE session_id IS NOT NULL
                    GROUP BY "StudentProgramProgressId", "WeekNumber", "DayNumber", "ExerciseId"
                    HAVING count(*) > 1) THEN
                    RAISE EXCEPTION 'Repeated task results exist; retain slot_order and roll back application/content only';
                  END IF;
                END $$;
                """);
            migrationBuilder.DropIndex(
                name: "ux_daily_exercise_logs_legacy_slot",
                schema: "speed_reading",
                table: "daily_exercise_logs");

            migrationBuilder.DropIndex(
                name: "ux_daily_exercise_logs_progress_slot",
                schema: "speed_reading",
                table: "daily_exercise_logs");

            migrationBuilder.DropColumn(
                name: "slot_order",
                schema: "speed_reading",
                table: "daily_exercise_logs");

            migrationBuilder.CreateIndex(
                name: "ux_daily_exercise_logs_progress_slot",
                schema: "speed_reading",
                table: "daily_exercise_logs",
                columns: new[] { "StudentProgramProgressId", "WeekNumber", "DayNumber", "ExerciseId" },
                unique: true,
                filter: "session_id IS NOT NULL");
        }
    }
}
