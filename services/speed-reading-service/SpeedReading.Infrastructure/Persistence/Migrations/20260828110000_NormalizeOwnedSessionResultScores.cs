using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OwnedSpeedReadingDbContext))]
[Migration("20260828110000_NormalizeOwnedSessionResultScores")]
public partial class NormalizeOwnedSessionResultScores : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "is_assessment_mode",
            schema: "speed_reading",
            table: "exercise_session_results",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.Sql(
            """
            UPDATE speed_reading.exercise_session_results
            SET score = LEAST(
                GREATEST(
                    (LEAST(GREATEST(comprehension_score, 0), 100) * 0.6)
                    + (LEAST(GREATEST(raw_wpm, 0) / 5, 100) * 0.4),
                    0),
                100);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Score values written by the previous migration had mixed semantics
        // and cannot be restored safely.
        migrationBuilder.DropColumn(
            name: "is_assessment_mode",
            schema: "speed_reading",
            table: "exercise_session_results");
    }
}
