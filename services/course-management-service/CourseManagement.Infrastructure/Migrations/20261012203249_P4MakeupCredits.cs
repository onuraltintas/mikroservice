using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CourseManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P4MakeupCredits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CancelledAt",
                table: "LessonParticipants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "LessonParticipants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "MakeupCredits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceLessonId = table.Column<Guid>(type: "uuid", nullable: false),
                    MakeupLessonId = table.Column<Guid>(type: "uuid", nullable: true),
                    GrantedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    GrantedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MakeupCredits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MakeupCredits_LessonSessions_MakeupLessonId",
                        column: x => x.MakeupLessonId,
                        principalTable: "LessonSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MakeupCredits_LessonSessions_SourceLessonId",
                        column: x => x.SourceLessonId,
                        principalTable: "LessonSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MakeupCredits_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MakeupCredits_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MakeupCredits_MakeupLessonId",
                table: "MakeupCredits",
                column: "MakeupLessonId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MakeupCredits_SourceLessonId",
                table: "MakeupCredits",
                column: "SourceLessonId");

            migrationBuilder.CreateIndex(
                name: "IX_MakeupCredits_StudentId",
                table: "MakeupCredits",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_MakeupCredits_WorkspaceId_SourceLessonId_StudentId",
                table: "MakeupCredits",
                columns: new[] { "WorkspaceId", "SourceLessonId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MakeupCredits_WorkspaceId_StudentId_Status_ExpiresAt",
                table: "MakeupCredits",
                columns: new[] { "WorkspaceId", "StudentId", "Status", "ExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MakeupCredits");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "LessonParticipants");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "LessonParticipants");
        }
    }
}
