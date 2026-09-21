using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCoachingErasureAssessments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CoachingErasureAssessments",
                schema: "coaching",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DryRun = table.Column<bool>(type: "boolean", nullable: false),
                    CanProceed = table.Column<bool>(type: "boolean", nullable: false),
                    HasActiveLegalHold = table.Column<bool>(type: "boolean", nullable: false),
                    AssignmentCount = table.Column<int>(type: "integer", nullable: false),
                    AttachmentCount = table.Column<int>(type: "integer", nullable: false),
                    ExamResultCount = table.Column<int>(type: "integer", nullable: false),
                    GoalCount = table.Column<int>(type: "integer", nullable: false),
                    SessionCount = table.Column<int>(type: "integer", nullable: false),
                    AgreementCount = table.Column<int>(type: "integer", nullable: false),
                    AssessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoachingErasureAssessments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CoachingLegalHolds",
                schema: "coaching",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    PlacedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlacedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReleasedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReleasedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoachingLegalHolds", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CoachingErasureAssessments_RequestId",
                schema: "coaching",
                table: "CoachingErasureAssessments",
                column: "RequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CoachingErasureAssessments_SubjectUserId_AssessedAt",
                schema: "coaching",
                table: "CoachingErasureAssessments",
                columns: new[] { "SubjectUserId", "AssessedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CoachingLegalHolds_SubjectUserId_ReleasedAt",
                schema: "coaching",
                table: "CoachingLegalHolds",
                columns: new[] { "SubjectUserId", "ReleasedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CoachingErasureAssessments",
                schema: "coaching");

            migrationBuilder.DropTable(
                name: "CoachingLegalHolds",
                schema: "coaching");
        }
    }
}
