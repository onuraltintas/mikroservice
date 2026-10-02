using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class LinkAcademicGoalTargetCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "target_school_id",
                schema: "coaching",
                table: "academic_goals",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "target_university_program_id",
                schema: "coaching",
                table: "academic_goals",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_academic_goals_target_school_id",
                schema: "coaching",
                table: "academic_goals",
                column: "target_school_id");

            migrationBuilder.CreateIndex(
                name: "IX_academic_goals_target_university_program_id",
                schema: "coaching",
                table: "academic_goals",
                column: "target_university_program_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_goal_single_catalog_target",
                schema: "coaching",
                table: "academic_goals",
                sql: "target_university_program_id IS NULL OR target_school_id IS NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_academic_goals_target_schools_target_school_id",
                schema: "coaching",
                table: "academic_goals",
                column: "target_school_id",
                principalSchema: "coaching",
                principalTable: "target_schools",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_academic_goals_target_university_programs_target_university~",
                schema: "coaching",
                table: "academic_goals",
                column: "target_university_program_id",
                principalSchema: "coaching",
                principalTable: "target_university_programs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_academic_goals_target_schools_target_school_id",
                schema: "coaching",
                table: "academic_goals");

            migrationBuilder.DropForeignKey(
                name: "FK_academic_goals_target_university_programs_target_university~",
                schema: "coaching",
                table: "academic_goals");

            migrationBuilder.DropIndex(
                name: "IX_academic_goals_target_school_id",
                schema: "coaching",
                table: "academic_goals");

            migrationBuilder.DropIndex(
                name: "IX_academic_goals_target_university_program_id",
                schema: "coaching",
                table: "academic_goals");

            migrationBuilder.DropCheckConstraint(
                name: "ck_goal_single_catalog_target",
                schema: "coaching",
                table: "academic_goals");

            migrationBuilder.DropColumn(
                name: "target_school_id",
                schema: "coaching",
                table: "academic_goals");

            migrationBuilder.DropColumn(
                name: "target_university_program_id",
                schema: "coaching",
                table: "academic_goals");
        }
    }
}
