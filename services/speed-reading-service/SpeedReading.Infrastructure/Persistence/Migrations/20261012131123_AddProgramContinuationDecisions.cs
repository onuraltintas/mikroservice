using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProgramContinuationDecisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "program_continuations",
                schema: "speed_reading",
                columns: table => new
                {
                    SourceProgressId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssessmentAttemptId = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecommendedLevel = table.Column<int>(type: "integer", nullable: false),
                    MeasuredLevel = table.Column<int>(type: "integer", nullable: false),
                    PolicyVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OverrideAcknowledged = table.Column<bool>(type: "boolean", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConfirmedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NewProgressId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_program_continuations", x => x.SourceProgressId);
                    table.ForeignKey(
                        name: "FK_program_continuations_assessment_attempts_AssessmentAttempt~",
                        column: x => x.AssessmentAttemptId,
                        principalSchema: "speed_reading",
                        principalTable: "assessment_attempts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_program_continuations_program_templates_TemplateId",
                        column: x => x.TemplateId,
                        principalSchema: "speed_reading",
                        principalTable: "program_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_program_continuations_student_program_progress_NewProgressId",
                        column: x => x.NewProgressId,
                        principalSchema: "speed_reading",
                        principalTable: "student_program_progress",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_program_continuations_student_program_progress_SourceProgre~",
                        column: x => x.SourceProgressId,
                        principalSchema: "speed_reading",
                        principalTable: "student_program_progress",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_program_continuations_AssessmentAttemptId",
                schema: "speed_reading",
                table: "program_continuations",
                column: "AssessmentAttemptId");

            migrationBuilder.CreateIndex(
                name: "IX_program_continuations_NewProgressId",
                schema: "speed_reading",
                table: "program_continuations",
                column: "NewProgressId");

            migrationBuilder.CreateIndex(
                name: "IX_program_continuations_StudentId_ConfirmedAt",
                schema: "speed_reading",
                table: "program_continuations",
                columns: new[] { "StudentId", "ConfirmedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_program_continuations_TemplateId",
                schema: "speed_reading",
                table: "program_continuations",
                column: "TemplateId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "program_continuations",
                schema: "speed_reading");
        }
    }
}
