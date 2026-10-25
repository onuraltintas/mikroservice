using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class WeeklyReviewsAndDecisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "weekly_decisions",
                schema: "coaching",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewWeekStart = table.Column<DateOnly>(type: "date", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    DueOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    HistoryJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_weekly_decisions", x => x.Id);
                    table.CheckConstraint("ck_weekly_decision_status", "\"Status\" IN ('Planned','InProgress','Completed','Cancelled') AND ((\"Status\"='Completed')=(\"CompletedAt\" IS NOT NULL))");
                    table.CheckConstraint("ck_weekly_decision_week", "extract(isodow from \"ReviewWeekStart\")=1 AND \"DueOn\" BETWEEN \"ReviewWeekStart\"+7 AND \"ReviewWeekStart\"+13");
                });

            migrationBuilder.CreateTable(
                name: "weekly_reflections",
                schema: "coaching",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    WeekStart = table.Column<DateOnly>(type: "date", nullable: false),
                    Successes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Obstacles = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    NextFocus = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Rating = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_weekly_reflections", x => x.Id);
                    table.CheckConstraint("ck_weekly_reflection_monday", "extract(isodow from \"WeekStart\")=1");
                    table.CheckConstraint("ck_weekly_reflection_rating", "\"Rating\" IS NULL OR \"Rating\" BETWEEN 1 AND 5");
                });

            migrationBuilder.CreateIndex(
                name: "IX_weekly_decisions_StudentId_ReviewWeekStart_Status",
                schema: "coaching",
                table: "weekly_decisions",
                columns: new[] { "StudentId", "ReviewWeekStart", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_weekly_decisions_TeacherId",
                schema: "coaching",
                table: "weekly_decisions",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_weekly_reflections_StudentId_WeekStart",
                schema: "coaching",
                table: "weekly_reflections",
                columns: new[] { "StudentId", "WeekStart" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "weekly_decisions",
                schema: "coaching");

            migrationBuilder.DropTable(
                name: "weekly_reflections",
                schema: "coaching");
        }
    }
}
