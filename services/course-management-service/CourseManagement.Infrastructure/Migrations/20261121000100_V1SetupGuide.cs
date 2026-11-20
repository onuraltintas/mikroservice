using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CourseManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class V1SetupGuide : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SetupClosedAt",
                table: "Workspaces",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SetupSkippedSteps",
                table: "Workspaces",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "TeacherEmailInvitations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OneToOneTeacherUserId",
                table: "Students",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Students_WorkspaceId_OneToOneTeacherUserId",
                table: "Students",
                columns: new[] { "WorkspaceId", "OneToOneTeacherUserId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Students_WorkspaceId_OneToOneTeacherUserId",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "SetupClosedAt",
                table: "Workspaces");

            migrationBuilder.DropColumn(
                name: "SetupSkippedSteps",
                table: "Workspaces");

            migrationBuilder.DropColumn(
                name: "DisplayName",
                table: "TeacherEmailInvitations");

            migrationBuilder.DropColumn(
                name: "OneToOneTeacherUserId",
                table: "Students");
        }
    }
}
