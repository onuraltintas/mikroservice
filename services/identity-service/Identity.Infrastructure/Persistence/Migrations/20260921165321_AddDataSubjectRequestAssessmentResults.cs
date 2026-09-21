using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDataSubjectRequestAssessmentResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DataSubjectRequestAssessmentResults",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
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
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataSubjectRequestAssessmentResults", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DataSubjectRequestAssessmentResults_RequestId_ServiceName",
                schema: "identity",
                table: "DataSubjectRequestAssessmentResults",
                columns: new[] { "RequestId", "ServiceName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DataSubjectRequestAssessmentResults_SubjectUserId_AssessedAt",
                schema: "identity",
                table: "DataSubjectRequestAssessmentResults",
                columns: new[] { "SubjectUserId", "AssessedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DataSubjectRequestAssessmentResults",
                schema: "identity");
        }
    }
}
