using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentReportedExamOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "created_by_teacher_id",
                schema: "coaching",
                table: "exams",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "student_owner_id",
                schema: "coaching",
                table: "exams",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "lesson_answers",
                schema: "coaching",
                table: "exam_results",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_exams_student_owner_id_exam_date",
                schema: "coaching",
                table: "exams",
                columns: new[] { "student_owner_id", "exam_date" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_exams_creator",
                schema: "coaching",
                table: "exams",
                sql: "(created_by_teacher_id IS NOT NULL AND student_owner_id IS NULL) OR (created_by_teacher_id IS NULL AND student_owner_id IS NOT NULL AND institution_id IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM coaching.exams WHERE student_owner_id IS NOT NULL) THEN
                        RAISE EXCEPTION 'Student-reported exams exist. Restore the backup or export these records before rollback.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropIndex(
                name: "IX_exams_student_owner_id_exam_date",
                schema: "coaching",
                table: "exams");

            migrationBuilder.DropCheckConstraint(
                name: "ck_exams_creator",
                schema: "coaching",
                table: "exams");

            migrationBuilder.DropColumn(
                name: "student_owner_id",
                schema: "coaching",
                table: "exams");

            migrationBuilder.DropColumn(
                name: "lesson_answers",
                schema: "coaching",
                table: "exam_results");

            migrationBuilder.AlterColumn<Guid>(
                name: "created_by_teacher_id",
                schema: "coaching",
                table: "exams",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
