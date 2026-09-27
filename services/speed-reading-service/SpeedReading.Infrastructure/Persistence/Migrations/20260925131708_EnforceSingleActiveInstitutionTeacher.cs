using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnforceSingleActiveInstitutionTeacher : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM speed_reading.teacher_student_assignments
                        WHERE institution_id IS NOT NULL AND is_active = TRUE
                        GROUP BY institution_id, student_user_id
                        HAVING COUNT(*) > 1
                    ) THEN
                        RAISE EXCEPTION 'Cannot enforce one active Speed Reading institution teacher per student: duplicate active assignments exist.';
                    END IF;
                END $$;
                """);

            migrationBuilder.CreateIndex(
                name: "ux_sra_institution_active_student_teacher",
                schema: "speed_reading",
                table: "teacher_student_assignments",
                columns: new[] { "institution_id", "student_user_id" },
                unique: true,
                filter: "institution_id IS NOT NULL AND is_active = true");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_sra_institution_active_student_teacher",
                schema: "speed_reading",
                table: "teacher_student_assignments");
        }
    }
}
