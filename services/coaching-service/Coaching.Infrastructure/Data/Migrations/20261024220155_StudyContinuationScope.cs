using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class StudyContinuationScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PredecessorTaskId",
                schema: "coaching",
                table: "study_plan_tasks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PredecessorVersion",
                schema: "coaching",
                table: "study_plan_tasks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RemainingMinutes",
                schema: "coaching",
                table: "study_plan_tasks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RemainingQuestions",
                schema: "coaching",
                table: "study_plan_tasks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_study_task_remaining",
                schema: "coaching",
                table: "study_plan_tasks",
                sql: "(\"RemainingMinutes\" IS NULL OR (NOT \"IsCompleted\" AND \"RemainingMinutes\" BETWEEN 1 AND 1440)) AND (\"RemainingQuestions\" IS NULL OR (\"RemainingMinutes\" IS NOT NULL AND \"RemainingQuestions\" BETWEEN 0 AND 100000)) AND ((\"PredecessorTaskId\" IS NULL AND \"PredecessorVersion\" IS NULL) OR (\"PredecessorTaskId\" IS NOT NULL AND \"PredecessorVersion\" >= 0 AND \"OriginalTaskId\" IS NOT NULL))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM coaching.study_plan_tasks WHERE "RemainingMinutes" IS NOT NULL OR "RemainingQuestions" IS NOT NULL OR "PredecessorTaskId" IS NOT NULL
                    OR EXISTS (SELECT 1 FROM jsonb_array_elements("RealizationHistoryJson") entry WHERE entry->>'RemainingMinutes' IS NOT NULL OR entry->>'PreviousRemainingMinutes' IS NOT NULL))
                  THEN RAISE EXCEPTION 'Continuation scope and lineage exist; export and migrate them explicitly before downgrade.'; END IF;
                END $$;
                """);
            migrationBuilder.DropCheckConstraint(
                name: "ck_study_task_remaining",
                schema: "coaching",
                table: "study_plan_tasks");

            migrationBuilder.DropColumn(
                name: "PredecessorTaskId",
                schema: "coaching",
                table: "study_plan_tasks");

            migrationBuilder.DropColumn(
                name: "PredecessorVersion",
                schema: "coaching",
                table: "study_plan_tasks");

            migrationBuilder.DropColumn(
                name: "RemainingMinutes",
                schema: "coaching",
                table: "study_plan_tasks");

            migrationBuilder.DropColumn(
                name: "RemainingQuestions",
                schema: "coaching",
                table: "study_plan_tasks");
        }
    }
}
