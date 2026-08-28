using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SpeedReading.Infrastructure.Persistence;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OwnedSpeedReadingDbContext))]
[Migration("20260828150000_PinAssessmentFormItems")]
public partial class PinAssessmentFormItems : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "assessment_attempt_exercises",
            schema: "speed_reading",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                assessment_attempt_id = table.Column<Guid>(type: "uuid", nullable: false),
                exercise_id = table.Column<Guid>(type: "uuid", nullable: false),
                reading_text_id = table.Column<Guid>(type: "uuid", nullable: true),
                role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                order_index = table.Column<int>(type: "integer", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                version = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_assessment_attempt_exercises", x => x.id);
                table.ForeignKey(
                    name: "fk_assessment_attempt_exercises_assessment_attempt_id",
                    column: x => x.assessment_attempt_id,
                    principalSchema: "speed_reading",
                    principalTable: "assessment_attempts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_assessment_attempt_exercises_exercise_id",
                    column: x => x.exercise_id,
                    principalSchema: "speed_reading",
                    principalTable: "exercises",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_assessment_attempt_exercises_reading_text_id",
                    column: x => x.reading_text_id,
                    principalSchema: "speed_reading",
                    principalTable: "reading_texts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ix_assessment_attempt_exercises_assessment_attempt_id_order_index",
            schema: "speed_reading",
            table: "assessment_attempt_exercises",
            columns: new[] { "assessment_attempt_id", "order_index" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_assessment_attempt_exercises_assessment_attempt_id_exercise_id",
            schema: "speed_reading",
            table: "assessment_attempt_exercises",
            columns: new[] { "assessment_attempt_id", "exercise_id" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "assessment_attempt_exercises",
            schema: "speed_reading");
    }
}
