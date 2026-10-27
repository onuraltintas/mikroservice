using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Roadmap O4: an institution can run one practice exam for all its students. Such an exam is created by an
    /// institution admin (no single teacher owns it) and records its practice scope (LGS, TYT, ...).
    /// </summary>
    public partial class InstitutionWideExams : Migration
    {
        private const string OldCreatorRule = "(created_by_teacher_id IS NOT NULL AND student_owner_id IS NULL) OR (created_by_teacher_id IS NULL AND student_owner_id IS NOT NULL AND institution_id IS NULL)";
        private const string NewCreatorRule = "(created_by_teacher_id IS NOT NULL AND student_owner_id IS NULL AND created_by_admin_id IS NULL) OR (created_by_teacher_id IS NULL AND student_owner_id IS NOT NULL AND institution_id IS NULL AND created_by_admin_id IS NULL) OR (created_by_admin_id IS NOT NULL AND created_by_teacher_id IS NULL AND student_owner_id IS NULL AND institution_id IS NOT NULL AND practice_exam_code IS NOT NULL)";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "created_by_admin_id",
                schema: "coaching",
                table: "exams",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "practice_exam_code",
                schema: "coaching",
                table: "exams",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.DropCheckConstraint(
                name: "ck_exams_creator",
                schema: "coaching",
                table: "exams");

            migrationBuilder.AddCheckConstraint(
                name: "ck_exams_creator",
                schema: "coaching",
                table: "exams",
                sql: NewCreatorRule);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Institution-wide exams cannot exist under the old rule; their results go with them.
            migrationBuilder.Sql("DELETE FROM coaching.exams WHERE created_by_admin_id IS NOT NULL;");

            migrationBuilder.DropCheckConstraint(
                name: "ck_exams_creator",
                schema: "coaching",
                table: "exams");

            migrationBuilder.AddCheckConstraint(
                name: "ck_exams_creator",
                schema: "coaching",
                table: "exams",
                sql: OldCreatorRule);

            migrationBuilder.DropColumn(
                name: "practice_exam_code",
                schema: "coaching",
                table: "exams");

            migrationBuilder.DropColumn(
                name: "created_by_admin_id",
                schema: "coaching",
                table: "exams");
        }
    }
}
