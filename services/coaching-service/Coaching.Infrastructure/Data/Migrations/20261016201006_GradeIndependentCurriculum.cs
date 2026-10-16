using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class GradeIndependentCurriculum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_outcome_edition_scope",
                schema: "coaching",
                table: "official_curriculum_editions");

            migrationBuilder.AlterColumn<int>(
                name: "GradeNumber",
                schema: "coaching",
                table: "official_curriculum_editions",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddCheckConstraint(
                name: "ck_outcome_edition_scope",
                schema: "coaching",
                table: "official_curriculum_editions",
                sql: "(\"GradeNumber\" IS NULL OR \"GradeNumber\" BETWEEN 1 AND 12) AND \"AcademicYearStart\" BETWEEN 2000 AND 2200 AND \"AcademicYearEnd\" BETWEEN \"AcademicYearStart\" AND 2200");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM coaching.official_curriculum_editions WHERE "GradeNumber" IS NULL)
                    THEN RAISE EXCEPTION 'Grade-independent programs exist; preserve them before reverting the migration.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropCheckConstraint(
                name: "ck_outcome_edition_scope",
                schema: "coaching",
                table: "official_curriculum_editions");

            migrationBuilder.AlterColumn<int>(
                name: "GradeNumber",
                schema: "coaching",
                table: "official_curriculum_editions",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_outcome_edition_scope",
                schema: "coaching",
                table: "official_curriculum_editions",
                sql: "\"GradeNumber\" BETWEEN 1 AND 12 AND \"AcademicYearStart\" BETWEEN 2000 AND 2200 AND \"AcademicYearEnd\" BETWEEN \"AcademicYearStart\" AND 2200");
        }
    }
}
