using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "coaching");

            migrationBuilder.CreateTable(
                name: "academic_goals",
                schema: "coaching",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    set_by_teacher_id = table.Column<Guid>(type: "uuid", nullable: true),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    target_exam_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    target_subject = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    target_score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    target_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    current_progress = table.Column<int>(type: "integer", nullable: false),
                    is_completed = table.Column<bool>(type: "boolean", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_academic_goals", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "assignments",
                schema: "coaching",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    teacher_id = table.Column<Guid>(type: "uuid", nullable: false),
                    institution_id = table.Column<Guid>(type: "uuid", nullable: true),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    subject = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    target_grade_level = table.Column<int>(type: "integer", nullable: true),
                    due_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    estimated_duration_minutes = table.Column<int>(type: "integer", nullable: true),
                    max_score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    passing_score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assignments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "coaching_sessions",
                schema: "coaching",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    teacher_id = table.Column<Guid>(type: "uuid", nullable: false),
                    institution_id = table.Column<Guid>(type: "uuid", nullable: true),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    session_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    scheduled_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    meeting_link = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    teacher_notes = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_coaching_sessions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "exams",
                schema: "coaching",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    institution_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by_teacher_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    exam_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    subject = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    exam_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    duration_minutes = table.Column<int>(type: "integer", nullable: true),
                    max_score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    target_grade_level = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exams", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "assignment_students",
                schema: "coaching",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    assignment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    teacher_feedback = table.Column<string>(type: "text", nullable: true),
                    student_note = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assignment_students", x => x.id);
                    table.ForeignKey(
                        name: "FK_assignment_students_assignments_assignment_id",
                        column: x => x.assignment_id,
                        principalSchema: "coaching",
                        principalTable: "assignments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "session_attendances",
                schema: "coaching",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attendance_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    joined_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    left_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    student_note = table.Column<string>(type: "text", nullable: true),
                    teacher_note = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_attendances", x => x.id);
                    table.ForeignKey(
                        name: "FK_session_attendances_coaching_sessions_session_id",
                        column: x => x.session_id,
                        principalSchema: "coaching",
                        principalTable: "coaching_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "exam_results",
                schema: "coaching",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    exam_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    correct_answers = table.Column<int>(type: "integer", nullable: true),
                    wrong_answers = table.Column<int>(type: "integer", nullable: true),
                    empty_answers = table.Column<int>(type: "integer", nullable: true),
                    subject_scores = table.Column<string>(type: "jsonb", nullable: true),
                    ranking = table.Column<int>(type: "integer", nullable: true),
                    teacher_notes = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exam_results", x => x.id);
                    table.ForeignKey(
                        name: "FK_exam_results_exams_exam_id",
                        column: x => x.exam_id,
                        principalSchema: "coaching",
                        principalTable: "exams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_academic_goals_is_completed",
                schema: "coaching",
                table: "academic_goals",
                column: "is_completed");

            migrationBuilder.CreateIndex(
                name: "ix_academic_goals_student_id",
                schema: "coaching",
                table: "academic_goals",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "ix_academic_goals_target_date",
                schema: "coaching",
                table: "academic_goals",
                column: "target_date");

            migrationBuilder.CreateIndex(
                name: "ix_assignment_students_assignment_student",
                schema: "coaching",
                table: "assignment_students",
                columns: new[] { "assignment_id", "student_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_assignment_students_status",
                schema: "coaching",
                table: "assignment_students",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_assignment_students_student_id",
                schema: "coaching",
                table: "assignment_students",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "ix_assignments_due_date",
                schema: "coaching",
                table: "assignments",
                column: "due_date");

            migrationBuilder.CreateIndex(
                name: "ix_assignments_institution_id",
                schema: "coaching",
                table: "assignments",
                column: "institution_id");

            migrationBuilder.CreateIndex(
                name: "ix_assignments_status",
                schema: "coaching",
                table: "assignments",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_assignments_teacher_id",
                schema: "coaching",
                table: "assignments",
                column: "teacher_id");

            migrationBuilder.CreateIndex(
                name: "ix_coaching_sessions_scheduled_date",
                schema: "coaching",
                table: "coaching_sessions",
                column: "scheduled_date");

            migrationBuilder.CreateIndex(
                name: "ix_coaching_sessions_status",
                schema: "coaching",
                table: "coaching_sessions",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_coaching_sessions_teacher_id",
                schema: "coaching",
                table: "coaching_sessions",
                column: "teacher_id");

            migrationBuilder.CreateIndex(
                name: "ix_exam_results_exam_student",
                schema: "coaching",
                table: "exam_results",
                columns: new[] { "exam_id", "student_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_exam_results_student_id",
                schema: "coaching",
                table: "exam_results",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "ix_exams_exam_date",
                schema: "coaching",
                table: "exams",
                column: "exam_date");

            migrationBuilder.CreateIndex(
                name: "ix_exams_exam_type",
                schema: "coaching",
                table: "exams",
                column: "exam_type");

            migrationBuilder.CreateIndex(
                name: "ix_exams_institution_id",
                schema: "coaching",
                table: "exams",
                column: "institution_id");

            migrationBuilder.CreateIndex(
                name: "ix_exams_teacher_id",
                schema: "coaching",
                table: "exams",
                column: "created_by_teacher_id");

            migrationBuilder.CreateIndex(
                name: "ix_session_attendances_session_student",
                schema: "coaching",
                table: "session_attendances",
                columns: new[] { "session_id", "student_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_session_attendances_student_id",
                schema: "coaching",
                table: "session_attendances",
                column: "student_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "academic_goals",
                schema: "coaching");

            migrationBuilder.DropTable(
                name: "assignment_students",
                schema: "coaching");

            migrationBuilder.DropTable(
                name: "exam_results",
                schema: "coaching");

            migrationBuilder.DropTable(
                name: "session_attendances",
                schema: "coaching");

            migrationBuilder.DropTable(
                name: "assignments",
                schema: "coaching");

            migrationBuilder.DropTable(
                name: "exams",
                schema: "coaching");

            migrationBuilder.DropTable(
                name: "coaching_sessions",
                schema: "coaching");
        }
    }
}
