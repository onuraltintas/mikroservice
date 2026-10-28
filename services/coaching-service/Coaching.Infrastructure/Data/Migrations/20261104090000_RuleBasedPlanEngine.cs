using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Roadmap O7: the rule-based plan engine stores each task's kind and reason, the topic's exam weight (1-5, default 3)
    /// and the student's own exam date. Existing rows keep NULL (tasks, exam date) or 3 (weight).
    /// </summary>
    public partial class RuleBasedPlanEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PlanKind",
                schema: "coaching",
                table: "study_plan_tasks",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlanReason",
                schema: "coaching",
                table: "study_plan_tasks",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "YieldWeight",
                schema: "coaching",
                table: "study_catalog_topics",
                type: "integer",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddColumn<DateOnly>(
                name: "TargetExamDate",
                schema: "coaching",
                table: "student_learning_contexts",
                type: "date",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_study_task_plan_kind",
                schema: "coaching",
                table: "study_plan_tasks",
                sql: "\"PlanKind\" IS NULL OR \"PlanKind\" IN ('Learn','Review','Refresh','Spaced')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_catalog_topic_yield",
                schema: "coaching",
                table: "study_catalog_topics",
                sql: "\"YieldWeight\" BETWEEN 1 AND 5");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(name: "ck_study_task_plan_kind", schema: "coaching", table: "study_plan_tasks");
            migrationBuilder.DropCheckConstraint(name: "ck_catalog_topic_yield", schema: "coaching", table: "study_catalog_topics");
            migrationBuilder.DropColumn(name: "PlanKind", schema: "coaching", table: "study_plan_tasks");
            migrationBuilder.DropColumn(name: "PlanReason", schema: "coaching", table: "study_plan_tasks");
            migrationBuilder.DropColumn(name: "YieldWeight", schema: "coaching", table: "study_catalog_topics");
            migrationBuilder.DropColumn(name: "TargetExamDate", schema: "coaching", table: "student_learning_contexts");
        }
    }
}
