using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CourseManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P2ExplicitStudentInvitationLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "StudentRecordId",
                table: "WorkspaceInvitations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceInvitations_StudentRecordId",
                table: "WorkspaceInvitations",
                column: "StudentRecordId");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkspaceInvitations_Students_StudentRecordId",
                table: "WorkspaceInvitations",
                column: "StudentRecordId",
                principalTable: "Students",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkspaceInvitations_Students_StudentRecordId",
                table: "WorkspaceInvitations");

            migrationBuilder.DropIndex(
                name: "IX_WorkspaceInvitations_StudentRecordId",
                table: "WorkspaceInvitations");

            migrationBuilder.DropColumn(
                name: "StudentRecordId",
                table: "WorkspaceInvitations");
        }
    }
}
