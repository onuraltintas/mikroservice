using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OwnedSpeedReadingDbContext))]
[Migration("20260828120000_AddMeasurementMetadata")]
public partial class AddMeasurementMetadata : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "is_measured",
            schema: "speed_reading",
            table: "exercise_session_results",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "is_measured",
            schema: "speed_reading",
            table: "daily_exercise_logs",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.Sql(
            """
            UPDATE speed_reading.exercise_session_results
            SET is_measured = jsonb_typeof(question_answers_json) = 'array'
                AND jsonb_array_length(question_answers_json) > 0;

            UPDATE speed_reading.daily_exercise_logs
            SET is_measured = "TotalAttempts" > 0
                OR "CorrectCount" > 0
                OR "IncorrectCount" > 0;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "is_measured",
            schema: "speed_reading",
            table: "daily_exercise_logs");

        migrationBuilder.DropColumn(
            name: "is_measured",
            schema: "speed_reading",
            table: "exercise_session_results");
    }
}
