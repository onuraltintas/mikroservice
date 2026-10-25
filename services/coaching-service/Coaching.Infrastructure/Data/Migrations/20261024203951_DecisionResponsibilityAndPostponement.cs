using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class DecisionResponsibilityAndPostponement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_weekly_decision_week",
                schema: "coaching",
                table: "weekly_decisions");

            migrationBuilder.AddColumn<bool>(
                name: "HasAssignmentScope",
                schema: "coaching",
                table: "weekly_decisions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateOnly>(
                name: "InitialDueOn",
                schema: "coaching",
                table: "weekly_decisions",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<Guid>(
                name: "InstitutionId",
                schema: "coaching",
                table: "weekly_decisions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ResponsibleTeacherId",
                schema: "coaching",
                table: "weekly_decisions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Preserve all pre-existing columns and JSON history; only initialize new metadata.
            migrationBuilder.Sql("""
                UPDATE coaching.weekly_decisions SET "ResponsibleTeacherId"="TeacherId",
                    "InitialDueOn"=COALESCE(("HistoryJson"->0->>'DueOn')::date,"DueOn");
                WITH scopes AS (
                    SELECT "TeacherUserId","StudentUserId",min("InstitutionId"::text)::uuid AS institution
                    FROM coaching.teacher_student_assignments
                    GROUP BY "TeacherUserId","StudentUserId"
                    HAVING count(DISTINCT coalesce("InstitutionId"::text,'independent'))=1
                )
                UPDATE coaching.weekly_decisions AS decision SET "InstitutionId"=scope.institution,"HasAssignmentScope"=TRUE
                FROM scopes AS scope WHERE decision."TeacherId"=scope."TeacherUserId" AND decision."StudentId"=scope."StudentUserId";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_weekly_decisions_ResponsibleTeacherId_StudentId_Status",
                schema: "coaching",
                table: "weekly_decisions",
                columns: new[] { "ResponsibleTeacherId", "StudentId", "Status" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_weekly_decision_week",
                schema: "coaching",
                table: "weekly_decisions",
                sql: "extract(isodow from \"ReviewWeekStart\")=1 AND \"DueOn\" >= \"ReviewWeekStart\"+7");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Downgrading must not silently restore an old teacher or truncate a postponed deadline.
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM coaching.weekly_decisions
                    WHERE "ResponsibleTeacherId"<>"TeacherId" OR "DueOn">"ReviewWeekStart"+13) THEN
                    RAISE EXCEPTION 'Cannot downgrade decisions with transferred responsibility or extended deadlines.';
                  END IF;
                END $$;
                """);
            migrationBuilder.DropIndex(
                name: "IX_weekly_decisions_ResponsibleTeacherId_StudentId_Status",
                schema: "coaching",
                table: "weekly_decisions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_weekly_decision_week",
                schema: "coaching",
                table: "weekly_decisions");

            migrationBuilder.DropColumn(
                name: "HasAssignmentScope",
                schema: "coaching",
                table: "weekly_decisions");

            migrationBuilder.DropColumn(
                name: "InitialDueOn",
                schema: "coaching",
                table: "weekly_decisions");

            migrationBuilder.DropColumn(
                name: "InstitutionId",
                schema: "coaching",
                table: "weekly_decisions");

            migrationBuilder.DropColumn(
                name: "ResponsibleTeacherId",
                schema: "coaching",
                table: "weekly_decisions");

            migrationBuilder.AddCheckConstraint(
                name: "ck_weekly_decision_week",
                schema: "coaching",
                table: "weekly_decisions",
                sql: "extract(isodow from \"ReviewWeekStart\")=1 AND \"DueOn\" BETWEEN \"ReviewWeekStart\"+7 AND \"ReviewWeekStart\"+13");
        }
    }
}
