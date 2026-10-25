using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class PreservePublishedPlanHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OriginalTaskId",
                schema: "coaching",
                table: "study_plan_tasks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "WasPublished",
                schema: "coaching",
                table: "study_plan_revisions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Only backfill publication when there is durable evidence. An archived
            // draft with no completions is indistinguishable from an unused old plan.
            migrationBuilder.Sql("""
                UPDATE coaching.study_plan_revisions AS plan
                SET "WasPublished" = TRUE
                WHERE plan."Status" = 1
                   OR (plan."Status" = 2 AND EXISTS (
                       SELECT 1 FROM coaching.study_plan_tasks AS task
                       WHERE task."RevisionId" = plan."Id"
                         AND task."StudentId" = plan."StudentId" AND task."IsCompleted"))
                   OR (plan."Status" = 2 AND EXISTS (
                       SELECT 1 FROM coaching.study_plan_revisions AS successor
                       WHERE successor."StudentId" = plan."StudentId" AND (
                           successor."AutomaticSourceRevisionId" = plan."Id"
                           OR successor."WeeklySource"->>'ActiveRevisionId' = plan."Id"::text)));
                """);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OriginalTaskId",
                schema: "coaching",
                table: "study_plan_tasks");

            migrationBuilder.DropColumn(
                name: "WasPublished",
                schema: "coaching",
                table: "study_plan_revisions");
        }
    }
}
