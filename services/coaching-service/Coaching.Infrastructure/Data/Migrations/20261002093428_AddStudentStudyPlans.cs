using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentStudyPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "study_plan_revisions",
                schema: "coaching",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    RevisionNumber = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_study_plan_revisions", x => x.Id);
                    table.UniqueConstraint("AK_study_plan_revisions_Id_StudentId", x => new { x.Id, x.StudentId });
                    table.CheckConstraint("ck_study_plan_revision_number", "\"RevisionNumber\" > 0");
                    table.CheckConstraint("ck_study_plan_status", "\"Status\" BETWEEN 0 AND 2 AND \"IsActive\" = (\"Status\" = 1)");
                });

            migrationBuilder.CreateTable(
                name: "study_plan_tasks",
                schema: "coaching",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    TopicId = table.Column<Guid>(type: "uuid", nullable: true),
                    PlannedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PlannedMinutes = table.Column<int>(type: "integer", nullable: false),
                    ActualMinutes = table.Column<int>(type: "integer", nullable: true),
                    IsPinned = table.Column<bool>(type: "boolean", nullable: false),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_study_plan_tasks", x => x.Id);
                    table.CheckConstraint("ck_study_task_completion", "(\"IsCompleted\" AND \"ActualMinutes\" IS NOT NULL AND \"CompletedAt\" IS NOT NULL) OR (NOT \"IsCompleted\" AND \"ActualMinutes\" IS NULL AND \"CompletedAt\" IS NULL)");
                    table.CheckConstraint("ck_study_task_minutes", "\"PlannedMinutes\" BETWEEN 1 AND 1440 AND (\"ActualMinutes\" IS NULL OR \"ActualMinutes\" BETWEEN 1 AND 1440)");
                    table.ForeignKey(
                        name: "FK_study_plan_tasks_study_catalog_topics_TopicId",
                        column: x => x.TopicId,
                        principalSchema: "coaching",
                        principalTable: "study_catalog_topics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_study_plan_tasks_study_plan_revisions_RevisionId_StudentId",
                        columns: x => new { x.RevisionId, x.StudentId },
                        principalSchema: "coaching",
                        principalTable: "study_plan_revisions",
                        principalColumns: new[] { "Id", "StudentId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_study_plan_revisions_StudentId_PlanId_RevisionNumber",
                schema: "coaching",
                table: "study_plan_revisions",
                columns: new[] { "StudentId", "PlanId", "RevisionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_study_plan_active_student",
                schema: "coaching",
                table: "study_plan_revisions",
                column: "StudentId",
                unique: true,
                filter: "\"IsActive\"");

            migrationBuilder.CreateIndex(
                name: "ux_study_plan_draft_student",
                schema: "coaching",
                table: "study_plan_revisions",
                column: "StudentId",
                unique: true,
                filter: "\"Status\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_study_plan_tasks_RevisionId_StudentId",
                schema: "coaching",
                table: "study_plan_tasks",
                columns: new[] { "RevisionId", "StudentId" });

            migrationBuilder.CreateIndex(
                name: "IX_study_plan_tasks_StudentId_PlannedDate",
                schema: "coaching",
                table: "study_plan_tasks",
                columns: new[] { "StudentId", "PlannedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_study_plan_tasks_TopicId",
                schema: "coaching",
                table: "study_plan_tasks",
                column: "TopicId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "study_plan_tasks",
                schema: "coaching");

            migrationBuilder.DropTable(
                name: "study_plan_revisions",
                schema: "coaching");
        }
    }
}
