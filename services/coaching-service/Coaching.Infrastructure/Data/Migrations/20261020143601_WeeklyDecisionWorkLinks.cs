using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class WeeklyDecisionWorkLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AssignmentId",
                schema: "coaching",
                table: "weekly_decisions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StudyTaskId",
                schema: "coaching",
                table: "weekly_decisions",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssignmentId",
                schema: "coaching",
                table: "weekly_decisions");

            migrationBuilder.DropColumn(
                name: "StudyTaskId",
                schema: "coaching",
                table: "weekly_decisions");
        }
    }
}
