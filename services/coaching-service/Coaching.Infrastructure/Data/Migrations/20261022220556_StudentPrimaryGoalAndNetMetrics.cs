using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class StudentPrimaryGoalAndNetMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NetTargetsJson",
                schema: "coaching",
                table: "academic_goals",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.CreateTable(
                name: "student_primary_goals",
                schema: "coaching",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    GoalId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_primary_goals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_student_primary_goals_academic_goals_GoalId",
                        column: x => x.GoalId,
                        principalSchema: "coaching",
                        principalTable: "academic_goals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_student_primary_goals_GoalId",
                schema: "coaching",
                table: "student_primary_goals",
                column: "GoalId");

            migrationBuilder.CreateIndex(
                name: "IX_student_primary_goals_StudentId",
                schema: "coaching",
                table: "student_primary_goals",
                column: "StudentId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "student_primary_goals",
                schema: "coaching");

            migrationBuilder.DropColumn(
                name: "NetTargetsJson",
                schema: "coaching",
                table: "academic_goals");
        }
    }
}
