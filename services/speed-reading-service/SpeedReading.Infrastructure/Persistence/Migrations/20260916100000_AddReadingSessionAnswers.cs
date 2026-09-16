using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OwnedSpeedReadingDbContext))]
[Migration("20260916100000_AddReadingSessionAnswers")]
public partial class AddReadingSessionAnswers : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "reading_session_answers",
            schema: "speed_reading",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                session_id = table.Column<Guid>(type: "uuid", nullable: false),
                question_id = table.Column<Guid>(type: "uuid", nullable: false),
                question_type = table.Column<int>(type: "integer", nullable: false),
                bloom_level = table.Column<int>(type: "integer", nullable: false),
                order_index = table.Column<int>(type: "integer", nullable: false),
                selected_answer = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                is_correct = table.Column<bool>(type: "boolean", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_reading_session_answers", x => x.id);
                table.ForeignKey(
                    name: "fk_reading_session_answers_reading_questions_question_id",
                    column: x => x.question_id,
                    principalSchema: "speed_reading",
                    principalTable: "reading_questions",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_reading_session_answers_reading_sessions_session_id",
                    column: x => x.session_id,
                    principalSchema: "speed_reading",
                    principalTable: "reading_sessions",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_reading_session_answers_question_id",
            schema: "speed_reading",
            table: "reading_session_answers",
            column: "question_id");
        migrationBuilder.CreateIndex(
            name: "ix_reading_session_answers_session_id_question_type",
            schema: "speed_reading",
            table: "reading_session_answers",
            columns: new[] { "session_id", "question_type" });
        migrationBuilder.CreateIndex(
            name: "ix_reading_session_answers_session_id_question_id",
            schema: "speed_reading",
            table: "reading_session_answers",
            columns: new[] { "session_id", "question_id" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "reading_session_answers",
            schema: "speed_reading");
    }
}
