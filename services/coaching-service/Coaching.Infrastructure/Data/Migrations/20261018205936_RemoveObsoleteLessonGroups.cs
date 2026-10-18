using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveObsoleteLessonGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                LOCK TABLE coaching.study_catalog_lesson_groups, coaching.study_catalog_lessons IN ACCESS EXCLUSIVE MODE;
                DO $$ BEGIN
                    IF EXISTS(SELECT 1 FROM coaching.study_catalog_lesson_groups)
                        OR EXISTS(SELECT 1 FROM coaching.study_catalog_lessons WHERE "GroupId" IS NOT NULL) THEN
                        RAISE EXCEPTION 'Resolve existing lesson groups before removing the obsolete schema.';
                    END IF;
                END $$;
                """);

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
                name: "GroupId",
                schema: "coaching",
                table: "study_catalog_lessons");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false)
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
    }
}
