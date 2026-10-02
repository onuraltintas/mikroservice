using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class TrackAutomaticStudyPlanSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AutomaticAvailabilityVersion",
                schema: "coaching",
                table: "study_plan_revisions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AutomaticSourceRevisionId",
                schema: "coaching",
                table: "study_plan_revisions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AutomaticSourceRevisionVersion",
                schema: "coaching",
                table: "study_plan_revisions",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutomaticAvailabilityVersion",
                schema: "coaching",
                table: "study_plan_revisions");

            migrationBuilder.DropColumn(
                name: "AutomaticSourceRevisionId",
                schema: "coaching",
                table: "study_plan_revisions");

            migrationBuilder.DropColumn(
                name: "AutomaticSourceRevisionVersion",
                schema: "coaching",
                table: "study_plan_revisions");
        }
    }
}
