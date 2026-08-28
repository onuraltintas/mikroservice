using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SpeedReading.Infrastructure.Persistence;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OwnedSpeedReadingDbContext))]
[Migration("20260828140000_LinkAssessmentAttemptsToSessions")]
public partial class LinkAssessmentAttemptsToSessions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "assessment_attempt_id",
            schema: "speed_reading",
            table: "exercise_sessions",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "assessment_attempt_id",
            schema: "speed_reading",
            table: "exercise_session_results",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "ix_exercise_sessions_assessment_attempt_id",
            schema: "speed_reading",
            table: "exercise_sessions",
            column: "assessment_attempt_id");

        migrationBuilder.CreateIndex(
            name: "ix_exercise_session_results_assessment_attempt_id",
            schema: "speed_reading",
            table: "exercise_session_results",
            column: "assessment_attempt_id");

        migrationBuilder.AddForeignKey(
            name: "fk_exercise_sessions_assessment_attempts_assessment_attempt_id",
            schema: "speed_reading",
            table: "exercise_sessions",
            column: "assessment_attempt_id",
            principalSchema: "speed_reading",
            principalTable: "assessment_attempts",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "fk_exercise_session_results_assessment_attempts_assessment_attempt_id",
            schema: "speed_reading",
            table: "exercise_session_results",
            column: "assessment_attempt_id",
            principalSchema: "speed_reading",
            principalTable: "assessment_attempts",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "fk_exercise_session_results_assessment_attempts_assessment_attempt_id",
            schema: "speed_reading",
            table: "exercise_session_results");

        migrationBuilder.DropForeignKey(
            name: "fk_exercise_sessions_assessment_attempts_assessment_attempt_id",
            schema: "speed_reading",
            table: "exercise_sessions");

        migrationBuilder.DropIndex(
            name: "ix_exercise_session_results_assessment_attempt_id",
            schema: "speed_reading",
            table: "exercise_session_results");

        migrationBuilder.DropIndex(
            name: "ix_exercise_sessions_assessment_attempt_id",
            schema: "speed_reading",
            table: "exercise_sessions");

        migrationBuilder.DropColumn(
            name: "assessment_attempt_id",
            schema: "speed_reading",
            table: "exercise_session_results");

        migrationBuilder.DropColumn(
            name: "assessment_attempt_id",
            schema: "speed_reading",
            table: "exercise_sessions");
    }
}
