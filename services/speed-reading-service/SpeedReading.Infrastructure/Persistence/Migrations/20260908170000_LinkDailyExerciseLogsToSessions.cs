using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OwnedSpeedReadingDbContext))]
[Migration("20260908170000_LinkDailyExerciseLogsToSessions")]
public partial class LinkDailyExerciseLogsToSessions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "session_id",
            schema: "speed_reading",
            table: "daily_exercise_logs",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "ux_daily_exercise_logs_user_session",
            schema: "speed_reading",
            table: "daily_exercise_logs",
            // The owned daily log table retains the historical PascalCase
            // property columns; session_id was added in this migration.
            columns: new[] { "UserId", "session_id" },
            unique: true,
            filter: "session_id IS NOT NULL");

    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ux_daily_exercise_logs_user_session",
            schema: "speed_reading",
            table: "daily_exercise_logs");


        migrationBuilder.DropColumn(
            name: "session_id",
            schema: "speed_reading",
            table: "daily_exercise_logs");
    }
}
