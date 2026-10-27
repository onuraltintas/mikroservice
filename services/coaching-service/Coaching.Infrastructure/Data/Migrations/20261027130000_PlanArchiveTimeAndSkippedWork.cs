using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Roadmap Y2: when a plan was replaced (its unfinished later days are no longer reported) and missed work that
    /// was skipped. Existing archived plans take their last update time, the closest record of when they were archived.
    /// </summary>
    public partial class PlanArchiveTimeAndSkippedWork : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                schema: "coaching",
                table: "study_plan_revisions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SkippedAt",
                schema: "coaching",
                table: "study_plan_tasks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql("UPDATE coaching.study_plan_revisions SET \"ArchivedAt\" = COALESCE(\"UpdatedAt\", \"CreatedAt\") WHERE \"Status\" = 2;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                schema: "coaching",
                table: "study_plan_revisions");

            migrationBuilder.DropColumn(
                name: "SkippedAt",
                schema: "coaching",
                table: "study_plan_tasks");
        }
    }
}
