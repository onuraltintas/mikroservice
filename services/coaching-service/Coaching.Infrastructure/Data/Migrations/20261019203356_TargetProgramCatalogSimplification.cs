using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class TargetProgramCatalogSimplification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_target_university_programs_Source_SourceId",
                schema: "coaching",
                table: "target_university_programs");

            migrationBuilder.DropCheckConstraint(
                name: "ck_target_program_score",
                schema: "coaching",
                table: "target_university_programs");

            migrationBuilder.DropColumn(
                name: "MinimumScore",
                schema: "coaching",
                table: "target_university_programs");

            migrationBuilder.DropColumn(
                name: "ScoreYear",
                schema: "coaching",
                table: "target_university_programs");

            migrationBuilder.DropColumn(
                name: "Source",
                schema: "coaching",
                table: "target_university_programs");

            migrationBuilder.DropColumn(
                name: "SourceId",
                schema: "coaching",
                table: "target_university_programs");

            migrationBuilder.Sql("""
                UPDATE coaching.target_university_programs
                SET "ProgramCode" = NULL
                WHERE "ProgramCode" IS NOT NULL AND BTRIM("ProgramCode") = '';

                CREATE TEMP TABLE target_program_duplicate_map ON COMMIT DROP AS
                WITH ranked AS (
                    SELECT p."Id",
                           FIRST_VALUE(p."Id") OVER (
                               PARTITION BY p."ProgramCode"
                               ORDER BY
                                   ((SELECT COUNT(*) FROM coaching.academic_goals g WHERE g.target_university_program_id = p."Id")
                                    + (SELECT COUNT(*) FROM coaching.target_admission_records r WHERE r."ProgramId" = p."Id")) DESC,
                                   p."IsActive" DESC,
                                   p."Id") AS "CanonicalId",
                           ROW_NUMBER() OVER (
                               PARTITION BY p."ProgramCode"
                               ORDER BY
                                   ((SELECT COUNT(*) FROM coaching.academic_goals g WHERE g.target_university_program_id = p."Id")
                                    + (SELECT COUNT(*) FROM coaching.target_admission_records r WHERE r."ProgramId" = p."Id")) DESC,
                                   p."IsActive" DESC,
                                   p."Id") AS "Position"
                    FROM coaching.target_university_programs p
                    WHERE p."ProgramCode" IS NOT NULL)
                SELECT "Id" AS "DuplicateId", "CanonicalId"
                FROM ranked
                WHERE "Position" > 1;

                UPDATE coaching.target_admission_records r
                SET "ProgramId" = m."CanonicalId"
                FROM target_program_duplicate_map m
                WHERE r."ProgramId" = m."DuplicateId";

                UPDATE coaching.academic_goals g
                SET target_university_program_id = m."CanonicalId"
                FROM target_program_duplicate_map m
                WHERE g.target_university_program_id = m."DuplicateId";

                UPDATE coaching.target_university_programs p
                SET "IsActive" = TRUE
                WHERE p."Id" IN (SELECT DISTINCT "CanonicalId" FROM target_program_duplicate_map);

                DELETE FROM coaching.target_university_programs p
                USING target_program_duplicate_map m
                WHERE p."Id" = m."DuplicateId";

                UPDATE coaching.target_university_programs
                SET "ScoreType" = CASE UPPER(BTRIM("ScoreType"))
                    WHEN 'DIL' THEN 'YDT'
                    WHEN 'DİL' THEN 'YDT'
                    WHEN 'YDT' THEN 'YDT'
                    WHEN 'SÖZ' THEN 'SOZ'
                    WHEN 'SOZ' THEN 'SOZ'
                    WHEN 'TYT' THEN 'TYT'
                    WHEN 'SAY' THEN 'SAY'
                    WHEN 'EA' THEN 'EA'
                    ELSE NULL
                END
                WHERE "ScoreType" IS NOT NULL;

                UPDATE coaching.target_admission_records
                SET "ScoreType" = CASE UPPER(BTRIM("ScoreType"))
                    WHEN 'DIL' THEN 'YDT'
                    WHEN 'DİL' THEN 'YDT'
                    WHEN 'YDT' THEN 'YDT'
                    WHEN 'SÖZ' THEN 'SOZ'
                    WHEN 'SOZ' THEN 'SOZ'
                    WHEN 'TYT' THEN 'TYT'
                    WHEN 'SAY' THEN 'SAY'
                    WHEN 'EA' THEN 'EA'
                    WHEN 'LGS' THEN 'LGS'
                    ELSE "ScoreType"
                END
                WHERE "ScoreType" IS NOT NULL;
                """);

            migrationBuilder.AddColumn<string>(
                name: "FacultyName",
                schema: "coaching",
                table: "target_university_programs",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProgramType",
                schema: "coaching",
                table: "target_university_programs",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_target_university_programs_ProgramCode",
                schema: "coaching",
                table: "target_university_programs",
                column: "ProgramCode",
                unique: true,
                filter: "\"ProgramCode\" IS NOT NULL AND \"IsActive\" = TRUE");

            migrationBuilder.AddCheckConstraint(
                name: "ck_target_program_score_type",
                schema: "coaching",
                table: "target_university_programs",
                sql: "\"ScoreType\" IS NULL OR \"ScoreType\" IN ('TYT','SAY','EA','SOZ','YDT')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_target_program_type",
                schema: "coaching",
                table: "target_university_programs",
                sql: "\"ProgramType\" IS NULL OR \"ProgramType\" IN ('Associate','Bachelor')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_target_university_programs_ProgramCode",
                schema: "coaching",
                table: "target_university_programs");

            migrationBuilder.DropCheckConstraint(
                name: "ck_target_program_score_type",
                schema: "coaching",
                table: "target_university_programs");

            migrationBuilder.DropCheckConstraint(
                name: "ck_target_program_type",
                schema: "coaching",
                table: "target_university_programs");

            migrationBuilder.DropColumn(
                name: "FacultyName",
                schema: "coaching",
                table: "target_university_programs");

            migrationBuilder.DropColumn(
                name: "ProgramType",
                schema: "coaching",
                table: "target_university_programs");

            migrationBuilder.AddColumn<decimal>(
                name: "MinimumScore",
                schema: "coaching",
                table: "target_university_programs",
                type: "numeric(10,4)",
                precision: 10,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ScoreYear",
                schema: "coaching",
                table: "target_university_programs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                schema: "coaching",
                table: "target_university_programs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SourceId",
                schema: "coaching",
                table: "target_university_programs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_target_university_programs_Source_SourceId",
                schema: "coaching",
                table: "target_university_programs",
                columns: new[] { "Source", "SourceId" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_target_program_score",
                schema: "coaching",
                table: "target_university_programs",
                sql: "\"MinimumScore\" IS NULL OR \"MinimumScore\" >= 0");
        }
    }
}
