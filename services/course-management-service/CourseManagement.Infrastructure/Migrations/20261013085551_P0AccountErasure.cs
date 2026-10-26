using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CourseManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P0AccountErasure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsErasureReadOnly",
                table: "Workspaces",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "AccountErasureAssessments",
                columns: table => new
                {
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CanProceed = table.Column<bool>(type: "boolean", nullable: false),
                    HasActiveLegalHold = table.Column<bool>(type: "boolean", nullable: false),
                    AssignmentCount = table.Column<int>(type: "integer", nullable: false),
                    AttachmentCount = table.Column<int>(type: "integer", nullable: false),
                    SessionCount = table.Column<int>(type: "integer", nullable: false),
                    AgreementCount = table.Column<int>(type: "integer", nullable: false),
                    AffectedRecordCount = table.Column<int>(type: "integer", nullable: false),
                    RecordCountsJson = table.Column<string>(type: "jsonb", nullable: false),
                    AssessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountErasureAssessments", x => x.RequestId);
                });

            migrationBuilder.CreateTable(
                name: "AccountErasureExecutions",
                columns: table => new
                {
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeletedRecordCount = table.Column<int>(type: "integer", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountErasureExecutions", x => x.RequestId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountErasureAssessments");

            migrationBuilder.DropTable(
                name: "AccountErasureExecutions");

            migrationBuilder.DropColumn(
                name: "IsErasureReadOnly",
                table: "Workspaces");
        }
    }
}
