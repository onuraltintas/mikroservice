using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSpeedReadingTeacherStudentAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "teacher_student_assignments",
                schema: "speed_reading",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    institution_id = table.Column<Guid>(type: "uuid", nullable: true),
                    teacher_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_teacher_student_assignments", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_teacher_student_assignments_student_user_id_is_active",
                schema: "speed_reading",
                table: "teacher_student_assignments",
                columns: new[] { "student_user_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_sra_institution_teacher_active",
                schema: "speed_reading",
                table: "teacher_student_assignments",
                columns: new[] { "institution_id", "teacher_user_id", "is_active" },
                filter: "institution_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_sra_institution_teacher_student",
                schema: "speed_reading",
                table: "teacher_student_assignments",
                columns: new[] { "institution_id", "teacher_user_id", "student_user_id" },
                unique: true,
                filter: "institution_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_sra_standalone_teacher_student",
                schema: "speed_reading",
                table: "teacher_student_assignments",
                columns: new[] { "teacher_user_id", "student_user_id" },
                unique: true,
                filter: "institution_id IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "teacher_student_assignments",
                schema: "speed_reading");
        }
    }
}
