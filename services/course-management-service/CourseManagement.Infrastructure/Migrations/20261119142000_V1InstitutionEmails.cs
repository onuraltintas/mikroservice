using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CourseManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class V1InstitutionEmails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EmailsMuted",
                table: "Students",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "CourseEmailOptOuts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseEmailOptOuts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CourseEmailOptOuts_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CourseEmails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    DedupKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ReferencesJson = table.Column<string>(type: "jsonb", nullable: true),
                    DueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Recipients = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RecipientCount = table.Column<int>(type: "integer", nullable: false),
                    SkipReason = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseEmails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CourseEmails_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CourseEmails_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkspaceEmailSettings",
                columns: table => new
                {
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    RulesJson = table.Column<string>(type: "jsonb", nullable: false),
                    InstallmentDaysBefore = table.Column<int>(type: "integer", nullable: false),
                    InstallmentOnDueDay = table.Column<bool>(type: "boolean", nullable: false),
                    OverdueDaysAfter = table.Column<int>(type: "integer", nullable: false),
                    SendHour = table.Column<int>(type: "integer", nullable: false),
                    ReplyToEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    InformedConsentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    InformedConsentByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkspaceEmailSettings", x => x.WorkspaceId);
                    table.ForeignKey(
                        name: "FK_WorkspaceEmailSettings_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CourseEmailOptOuts_WorkspaceId_Email",
                table: "CourseEmailOptOuts",
                columns: new[] { "WorkspaceId", "Email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CourseEmails_Status_DueAt",
                table: "CourseEmails",
                columns: new[] { "Status", "DueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CourseEmails_StudentId",
                table: "CourseEmails",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseEmails_WorkspaceId_CreatedAt",
                table: "CourseEmails",
                columns: new[] { "WorkspaceId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CourseEmails_WorkspaceId_DedupKey",
                table: "CourseEmails",
                columns: new[] { "WorkspaceId", "DedupKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CourseEmailOptOuts");

            migrationBuilder.DropTable(
                name: "CourseEmails");

            migrationBuilder.DropTable(
                name: "WorkspaceEmailSettings");

            migrationBuilder.DropColumn(
                name: "EmailsMuted",
                table: "Students");
        }
    }
}
