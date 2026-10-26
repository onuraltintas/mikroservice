using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class PracticeTopicAdaptation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByTeacherId",
                schema: "coaching",
                table: "study_plan_revisions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "topic_analysis",
                schema: "coaching",
                table: "exam_results",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedByTeacherId",
                schema: "coaching",
                table: "study_plan_revisions");

            migrationBuilder.DropColumn(
                name: "topic_analysis",
                schema: "coaching",
                table: "exam_results");
        }
    }
}
