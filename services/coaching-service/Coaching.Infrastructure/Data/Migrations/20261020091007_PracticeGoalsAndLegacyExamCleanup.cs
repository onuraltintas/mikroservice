using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class PracticeGoalsAndLegacyExamCleanup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Only legacy LGS/YKS results lacking the new validated breakdown are obsolete.
            // School/weekly/branch exams and current practice results remain in use.
            migrationBuilder.Sql("""
                WITH removed AS (
                  DELETE FROM coaching.exam_results r USING coaching.exams e
                  WHERE r.exam_id=e.id AND e.exam_type IN ('LGS','YKS') AND r.practice_exam IS NULL
                  RETURNING r.id
                ) DELETE FROM coaching."IdempotencyRecords" i USING removed r
                  WHERE i."ResourceId"=r.id AND i."Scope"='coaching.exam-results.create';
                WITH removed AS (
                  DELETE FROM coaching.exams e WHERE e.exam_type IN ('LGS','YKS')
                    AND (e.student_owner_id IS NOT NULL OR e.exam_date < now())
                    AND NOT EXISTS(SELECT 1 FROM coaching.exam_results r WHERE r.exam_id=e.id)
                  RETURNING e.id
                ) DELETE FROM coaching."IdempotencyRecords" i USING removed e
                  WHERE i."ResourceId"=e.id AND i."Scope"='coaching.exams.create';
                """);
            migrationBuilder.AddColumn<decimal>(
                name: "target_net",
                schema: "coaching",
                table: "academic_goals",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "target_practice_exam_code",
                schema: "coaching",
                table: "academic_goals",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_goal_net_target",
                schema: "coaching",
                table: "academic_goals",
                sql: "(target_net IS NULL AND target_practice_exam_code IS NULL) OR (target_net IS NOT NULL AND target_practice_exam_code IS NOT NULL AND target_net >= 0 AND target_net <= CASE target_practice_exam_code WHEN 'LGS' THEN 90 WHEN 'TYT' THEN 120 WHEN 'AYT_SAY' THEN 80 WHEN 'AYT_EA' THEN 80 WHEN 'AYT_SOZ' THEN 80 WHEN 'YDT_EN' THEN 80 ELSE -1 END)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_goal_net_target",
                schema: "coaching",
                table: "academic_goals");

            migrationBuilder.DropColumn(
                name: "target_net",
                schema: "coaching",
                table: "academic_goals");

            migrationBuilder.DropColumn(
                name: "target_practice_exam_code",
                schema: "coaching",
                table: "academic_goals");
        }
    }
}
