using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OwnedSpeedReadingDbContext))]
[Migration("20260916102000_AddExerciseAnswerQuestionType")]
public partial class AddExerciseAnswerQuestionType : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "question_type",
            schema: "speed_reading",
            table: "exercise_session_answers",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.CreateIndex(
            name: "ix_exercise_session_answers_session_id_question_type",
            schema: "speed_reading",
            table: "exercise_session_answers",
            columns: new[] { "session_id", "question_type" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_exercise_session_answers_session_id_question_type",
            schema: "speed_reading",
            table: "exercise_session_answers");

        migrationBuilder.DropColumn(
            name: "question_type",
            schema: "speed_reading",
            table: "exercise_session_answers");
    }
}
