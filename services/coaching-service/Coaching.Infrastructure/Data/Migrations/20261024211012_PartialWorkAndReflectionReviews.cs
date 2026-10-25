using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class PartialWorkAndReflectionReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_study_task_completion",
                schema: "coaching",
                table: "study_plan_tasks");

            migrationBuilder.DropCheckConstraint(
                name: "ck_study_task_questions",
                schema: "coaching",
                table: "study_plan_tasks");

            migrationBuilder.AddColumn<string>(
                name: "RealizationHistoryJson",
                schema: "coaching",
                table: "study_plan_tasks",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.CreateTable(
                name: "weekly_reflection_reviews",
                schema: "coaching",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReflectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReflectionVersion = table.Column<int>(type: "integer", nullable: false),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ReflectionSnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_weekly_reflection_reviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_weekly_reflection_reviews_weekly_reflections_ReflectionId",
                        column: x => x.ReflectionId,
                        principalSchema: "coaching",
                        principalTable: "weekly_reflections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_study_task_completion",
                schema: "coaching",
                table: "study_plan_tasks",
                sql: "(\"IsCompleted\" AND \"ActualMinutes\" IS NOT NULL AND \"CompletedAt\" IS NOT NULL) OR (NOT \"IsCompleted\" AND \"CompletedAt\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_study_task_questions",
                schema: "coaching",
                table: "study_plan_tasks",
                sql: "(\"PlannedQuestions\" IS NULL OR \"PlannedQuestions\" BETWEEN 0 AND 100000) AND (\"ActualQuestions\" IS NULL OR \"ActualQuestions\" BETWEEN 0 AND 100000) AND (\"ActualQuestions\" IS NULL OR \"ActualMinutes\" IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_weekly_reflection_reviews_ReflectionId_TeacherId_Reflection~",
                schema: "coaching",
                table: "weekly_reflection_reviews",
                columns: new[] { "ReflectionId", "TeacherId", "ReflectionVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_weekly_reflection_reviews_StudentId",
                schema: "coaching",
                table: "weekly_reflection_reviews",
                column: "StudentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Refuse to discard work/audit/review data on rollback.
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM coaching.study_plan_tasks WHERE "RealizationHistoryJson" <> '[]'::jsonb OR (NOT "IsCompleted" AND "ActualMinutes" IS NOT NULL))
                    OR EXISTS (SELECT 1 FROM coaching.weekly_reflection_reviews)
                  THEN RAISE EXCEPTION 'Partial work, realization history or reflection reviews exist; export and migrate them explicitly before downgrade.';
                  END IF;
                END $$;
                """);
            migrationBuilder.DropTable(
                name: "weekly_reflection_reviews",
                schema: "coaching");

            migrationBuilder.DropCheckConstraint(
                name: "ck_study_task_completion",
                schema: "coaching",
                table: "study_plan_tasks");

            migrationBuilder.DropCheckConstraint(
                name: "ck_study_task_questions",
                schema: "coaching",
                table: "study_plan_tasks");

            migrationBuilder.DropColumn(
                name: "RealizationHistoryJson",
                schema: "coaching",
                table: "study_plan_tasks");

            migrationBuilder.AddCheckConstraint(
                name: "ck_study_task_completion",
                schema: "coaching",
                table: "study_plan_tasks",
                sql: "(\"IsCompleted\" AND \"ActualMinutes\" IS NOT NULL AND \"CompletedAt\" IS NOT NULL) OR (NOT \"IsCompleted\" AND \"ActualMinutes\" IS NULL AND \"CompletedAt\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_study_task_questions",
                schema: "coaching",
                table: "study_plan_tasks",
                sql: "(\"PlannedQuestions\" IS NULL OR \"PlannedQuestions\" BETWEEN 0 AND 100000) AND (\"ActualQuestions\" IS NULL OR \"ActualQuestions\" BETWEEN 0 AND 100000) AND (\"IsCompleted\" OR \"ActualQuestions\" IS NULL)");
        }
    }
}
