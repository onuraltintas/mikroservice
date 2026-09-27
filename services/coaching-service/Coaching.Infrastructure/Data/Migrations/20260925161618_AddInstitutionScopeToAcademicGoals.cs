using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInstitutionScopeToAcademicGoals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "institution_id",
                schema: "coaching",
                table: "academic_goals",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_academic_goals_institution_student",
                schema: "coaching",
                table: "academic_goals",
                columns: new[] { "institution_id", "student_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_academic_goals_institution_student",
                schema: "coaching",
                table: "academic_goals");

            migrationBuilder.DropColumn(
                name: "institution_id",
                schema: "coaching",
                table: "academic_goals");
        }
    }
}
