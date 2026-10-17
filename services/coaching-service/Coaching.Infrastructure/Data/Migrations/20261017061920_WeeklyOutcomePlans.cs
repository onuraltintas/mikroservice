using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class WeeklyOutcomePlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LessonNameSnapshot",
                schema: "coaching",
                table: "study_plan_tasks",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PlannedStartMinute",
                schema: "coaching",
                table: "study_plan_tasks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WeeklyLessonId",
                schema: "coaching",
                table: "study_plan_tasks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WeeklySource",
                schema: "coaching",
                table: "study_plan_revisions",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_study_task_weekly_time",
                schema: "coaching",
                table: "study_plan_tasks",
                sql: "(\"PlannedStartMinute\" IS NULL AND \"WeeklyLessonId\" IS NULL AND \"LessonNameSnapshot\" IS NULL) OR (\"PlannedStartMinute\" BETWEEN 0 AND 1439 AND \"PlannedStartMinute\"+\"PlannedMinutes\"<=1440 AND \"WeeklyLessonId\" IS NOT NULL AND \"LessonNameSnapshot\" IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_study_task_weekly_time",
                schema: "coaching",
                table: "study_plan_tasks");

            migrationBuilder.DropColumn(
                name: "LessonNameSnapshot",
                schema: "coaching",
                table: "study_plan_tasks");

            migrationBuilder.DropColumn(
                name: "PlannedStartMinute",
                schema: "coaching",
                table: "study_plan_tasks");

            migrationBuilder.DropColumn(
                name: "WeeklyLessonId",
                schema: "coaching",
                table: "study_plan_tasks");

            migrationBuilder.DropColumn(
                name: "WeeklySource",
                schema: "coaching",
                table: "study_plan_revisions");
        }
    }
}
