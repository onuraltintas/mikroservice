using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class StudyProgressReconciliation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_study_task_completion",
                schema: "coaching",
                table: "study_plan_tasks");

            migrationBuilder.AddColumn<Guid>(
                name: "CompletionSourceTaskId",
                schema: "coaching",
                table: "study_plan_tasks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_study_task_completion",
                schema: "coaching",
                table: "study_plan_tasks",
                sql: "(\"IsCompleted\" AND (\"ActualMinutes\" IS NOT NULL OR \"CompletionSourceTaskId\" IS NOT NULL) AND \"CompletedAt\" IS NOT NULL) OR (NOT \"IsCompleted\" AND \"CompletedAt\" IS NULL AND \"CompletionSourceTaskId\" IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM coaching.study_plan_tasks WHERE "CompletionSourceTaskId" IS NOT NULL)
                  THEN RAISE EXCEPTION 'Reconciled completion evidence exists; preserve work-chain state before downgrade.'; END IF;
                END $$;
                """);
            migrationBuilder.DropCheckConstraint(
                name: "ck_study_task_completion",
                schema: "coaching",
                table: "study_plan_tasks");

            migrationBuilder.DropColumn(
                name: "CompletionSourceTaskId",
                schema: "coaching",
                table: "study_plan_tasks");

            migrationBuilder.AddCheckConstraint(
                name: "ck_study_task_completion",
                schema: "coaching",
                table: "study_plan_tasks",
                sql: "(\"IsCompleted\" AND \"ActualMinutes\" IS NOT NULL AND \"CompletedAt\" IS NOT NULL) OR (NOT \"IsCompleted\" AND \"CompletedAt\" IS NULL)");
        }
    }
}
