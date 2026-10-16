using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class CurriculumManagementHierarchy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CodeSnapshot",
                schema: "coaching",
                table: "study_plan_task_outcomes",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameSnapshot",
                schema: "coaching",
                table: "study_plan_task_outcomes",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE coaching.study_plan_task_outcomes AS link
                SET "CodeSnapshot" = outcome."Code", "NameSnapshot" = outcome."Name"
                FROM coaching.study_catalog_outcomes AS outcome
                WHERE outcome."Id" = link."OutcomeId";
                """);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                schema: "coaching",
                table: "study_catalog_outcomes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "GroupId",
                schema: "coaching",
                table: "study_catalog_lessons",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "study_catalog_lesson_groups",
                schema: "coaching",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_study_catalog_lesson_groups", x => x.Id);
                    table.CheckConstraint("ck_lesson_group_order", "\"DisplayOrder\" >= 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_study_catalog_lessons_GroupId",
                schema: "coaching",
                table: "study_catalog_lessons",
                column: "GroupId");

            migrationBuilder.AddForeignKey(
                name: "FK_study_catalog_lessons_study_catalog_lesson_groups_GroupId",
                schema: "coaching",
                table: "study_catalog_lessons",
                column: "GroupId",
                principalSchema: "coaching",
                principalTable: "study_catalog_lesson_groups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_study_catalog_lessons_study_catalog_lesson_groups_GroupId",
                schema: "coaching",
                table: "study_catalog_lessons");

            migrationBuilder.DropTable(
                name: "study_catalog_lesson_groups",
                schema: "coaching");

            migrationBuilder.DropIndex(
                name: "IX_study_catalog_lessons_GroupId",
                schema: "coaching",
                table: "study_catalog_lessons");

            migrationBuilder.DropColumn(
                name: "CodeSnapshot",
                schema: "coaching",
                table: "study_plan_task_outcomes");

            migrationBuilder.DropColumn(
                name: "NameSnapshot",
                schema: "coaching",
                table: "study_plan_task_outcomes");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                schema: "coaching",
                table: "study_catalog_outcomes");

            migrationBuilder.DropColumn(
                name: "GroupId",
                schema: "coaching",
                table: "study_catalog_lessons");
        }
    }
}
