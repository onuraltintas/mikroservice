using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ActualStudyWorkDates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActualWorkJson",
                schema: "coaching",
                table: "study_plan_tasks",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "WorkFromDate",
                schema: "coaching",
                table: "study_plan_tasks",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "WorkToDate",
                schema: "coaching",
                table: "study_plan_tasks",
                type: "date",
                nullable: true);

            // Preserve the old attribution without inventing a previously unrecorded actual work date.
            migrationBuilder.Sql("""
                UPDATE coaching.study_plan_tasks SET "WorkFromDate"="PlannedDate","WorkToDate"="PlannedDate"
                WHERE "ActualMinutes" IS NOT NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_study_plan_tasks_StudentId_WorkFromDate_WorkToDate",
                schema: "coaching",
                table: "study_plan_tasks",
                columns: new[] { "StudentId", "WorkFromDate", "WorkToDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM coaching.study_plan_tasks WHERE "ActualWorkJson" IS NOT NULL
                    OR "WorkFromDate" IS DISTINCT FROM "PlannedDate" AND "ActualMinutes" IS NOT NULL)
                  THEN RAISE EXCEPTION 'Actual work dates exist; preserve dated work before downgrade.'; END IF;
                END $$;
                """);

            migrationBuilder.DropIndex(
                name: "IX_study_plan_tasks_StudentId_WorkFromDate_WorkToDate",
                schema: "coaching",
                table: "study_plan_tasks");

            migrationBuilder.DropColumn(
                name: "ActualWorkJson",
                schema: "coaching",
                table: "study_plan_tasks");

            migrationBuilder.DropColumn(
                name: "WorkFromDate",
                schema: "coaching",
                table: "study_plan_tasks");

            migrationBuilder.DropColumn(
                name: "WorkToDate",
                schema: "coaching",
                table: "study_plan_tasks");
        }
    }
}
