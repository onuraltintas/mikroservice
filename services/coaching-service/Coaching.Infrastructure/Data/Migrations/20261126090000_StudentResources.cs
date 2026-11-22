using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <summary>
    /// KocApp uyumu (22.11.2026): the student's books and sets (student_resources) and, on a study task, the resource range it
    /// works through; a resource belongs to a catalog lesson and an exam when they are chosen. The practice-exam review key ("DenemeTekrar:…") that was written into the student-facing "Kaynak / sayfa"
    /// field (SourceName) moves to its own column AdviceKey; Down moves it back. Nothing else changes.
    /// </summary>
    public partial class StudentResources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "student_resources",
                schema: "coaching",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Publisher = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    LessonName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LessonId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExamCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TotalUnits = table.Column<int>(type: "integer", nullable: false),
                    DoneBefore = table.Column<int>(type: "integer", nullable: false),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                    SectionsJson = table.Column<string>(type: "jsonb", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_resources", x => x.Id);
                    table.CheckConstraint("ck_student_resource_kind", "\"Kind\" IN ('QuestionBank','TopicBook','PracticeExams','Other') AND \"Unit\" IN ('Page','Test','Question')");
                    table.CheckConstraint("ck_student_resource_sections", "jsonb_typeof(\"SectionsJson\") = 'array'");
                    table.CheckConstraint("ck_student_resource_units", "\"TotalUnits\" BETWEEN 1 AND 10000 AND \"DoneBefore\" BETWEEN 0 AND \"TotalUnits\"");
                });

            migrationBuilder.CreateIndex(
                name: "IX_student_resources_StudentId_IsArchived",
                schema: "coaching",
                table: "student_resources",
                columns: new[] { "StudentId", "IsArchived" });

            migrationBuilder.AddColumn<string>(
                name: "AdviceKey",
                schema: "coaching",
                table: "study_plan_tasks",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResourceFrom",
                schema: "coaching",
                table: "study_plan_tasks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ResourceId",
                schema: "coaching",
                table: "study_plan_tasks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResourceTo",
                schema: "coaching",
                table: "study_plan_tasks",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_study_plan_tasks_StudentId_ResourceId",
                schema: "coaching",
                table: "study_plan_tasks",
                columns: new[] { "StudentId", "ResourceId" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_study_task_resource",
                schema: "coaching",
                table: "study_plan_tasks",
                sql: "(\"ResourceFrom\" IS NULL AND \"ResourceTo\" IS NULL) OR (\"ResourceId\" IS NOT NULL AND \"ResourceFrom\" >= 1 AND \"ResourceTo\" >= \"ResourceFrom\")");

            // The review key leaves the student's "Kaynak / sayfa" field. The key is "DenemeTekrar:" and 32 hex characters (45).
            migrationBuilder.Sql("UPDATE coaching.study_plan_tasks SET \"AdviceKey\" = \"SourceName\", \"SourceName\" = NULL WHERE \"SourceName\" LIKE 'DenemeTekrar:%' AND length(\"SourceName\") <= 64;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE coaching.study_plan_tasks SET \"SourceName\" = \"AdviceKey\" WHERE \"AdviceKey\" IS NOT NULL AND \"SourceName\" IS NULL;");

            migrationBuilder.DropCheckConstraint(
                name: "ck_study_task_resource",
                schema: "coaching",
                table: "study_plan_tasks");

            migrationBuilder.DropIndex(
                name: "IX_study_plan_tasks_StudentId_ResourceId",
                schema: "coaching",
                table: "study_plan_tasks");

            migrationBuilder.DropColumn(
                name: "AdviceKey",
                schema: "coaching",
                table: "study_plan_tasks");

            migrationBuilder.DropColumn(
                name: "ResourceFrom",
                schema: "coaching",
                table: "study_plan_tasks");

            migrationBuilder.DropColumn(
                name: "ResourceId",
                schema: "coaching",
                table: "study_plan_tasks");

            migrationBuilder.DropColumn(
                name: "ResourceTo",
                schema: "coaching",
                table: "study_plan_tasks");

            migrationBuilder.DropTable(
                name: "student_resources",
                schema: "coaching");
        }
    }
}
