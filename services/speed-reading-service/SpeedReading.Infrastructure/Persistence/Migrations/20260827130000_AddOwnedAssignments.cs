using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OwnedSpeedReadingDbContext))]
[Migration("20260827130000_AddOwnedAssignments")]
public partial class AddOwnedAssignments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "assignments",
            schema: "speed_reading",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                TeacherId = table.Column<Guid>(type: "uuid", nullable: false),
                ExerciseId = table.Column<Guid>(type: "uuid", nullable: false),
                ReadingTextId = table.Column<Guid>(type: "uuid", nullable: true),
                Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                version = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_assignments", x => x.id);
                table.ForeignKey(
                    name: "fk_assignments_exercises_exercise_id",
                    column: x => x.ExerciseId,
                    principalSchema: "speed_reading",
                    principalTable: "exercises",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_assignments_reading_texts_reading_text_id",
                    column: x => x.ReadingTextId,
                    principalSchema: "speed_reading",
                    principalTable: "reading_texts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "student_assignments",
            schema: "speed_reading",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                AssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                IsCompleted = table.Column<bool>(type: "boolean", nullable: false),
                CompletionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                ResultId = table.Column<Guid>(type: "uuid", nullable: true),
                Score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                KeyPerformanceMetric = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_student_assignments", x => x.id);
                table.ForeignKey(
                    name: "fk_student_assignments_assignments_assignment_id",
                    column: x => x.AssignmentId,
                    principalSchema: "speed_reading",
                    principalTable: "assignments",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_assignments_exercise_id_is_active",
            schema: "speed_reading",
            table: "assignments",
            columns: new[] { "ExerciseId", "IsActive" });
        migrationBuilder.CreateIndex(
            name: "ix_assignments_reading_text_id",
            schema: "speed_reading",
            table: "assignments",
            column: "ReadingTextId");
        migrationBuilder.CreateIndex(
            name: "ix_assignments_teacher_id_created_at",
            schema: "speed_reading",
            table: "assignments",
            columns: new[] { "TeacherId", "created_at" });
        migrationBuilder.CreateIndex(
            name: "ix_student_assignments_assignment_id_student_id",
            schema: "speed_reading",
            table: "student_assignments",
            columns: new[] { "AssignmentId", "StudentId" },
            unique: true,
            filter: "\"IsActive\" = TRUE");
        migrationBuilder.CreateIndex(
            name: "ix_student_assignments_result_id",
            schema: "speed_reading",
            table: "student_assignments",
            column: "ResultId");
        migrationBuilder.CreateIndex(
            name: "ix_student_assignments_student_id_is_active_created_at",
            schema: "speed_reading",
            table: "student_assignments",
            columns: new[] { "StudentId", "IsActive", "created_at" });
        migrationBuilder.CreateIndex(
            name: "ix_exercise_sessions_StudentAssignmentId",
            schema: "speed_reading",
            table: "exercise_sessions",
            column: "StudentAssignmentId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "student_assignments", schema: "speed_reading");
        migrationBuilder.DropTable(name: "assignments", schema: "speed_reading");
    }
}
