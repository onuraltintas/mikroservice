using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStudyTaskQuestionTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ActualQuestions",
                schema: "coaching",
                table: "study_plan_tasks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PlannedQuestions",
                schema: "coaching",
                table: "study_plan_tasks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceName",
                schema: "coaching",
                table: "study_plan_tasks",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_study_task_questions",
                schema: "coaching",
                table: "study_plan_tasks",
                sql: "(\"PlannedQuestions\" IS NULL OR \"PlannedQuestions\" BETWEEN 0 AND 100000) AND (\"ActualQuestions\" IS NULL OR \"ActualQuestions\" BETWEEN 0 AND 100000) AND (\"IsCompleted\" OR \"ActualQuestions\" IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_study_task_questions",
                schema: "coaching",
                table: "study_plan_tasks");

            migrationBuilder.DropColumn(
                name: "ActualQuestions",
                schema: "coaching",
                table: "study_plan_tasks");

            migrationBuilder.DropColumn(
                name: "PlannedQuestions",
                schema: "coaching",
                table: "study_plan_tasks");

            migrationBuilder.DropColumn(
                name: "SourceName",
                schema: "coaching",
                table: "study_plan_tasks");
        }
    }
}
