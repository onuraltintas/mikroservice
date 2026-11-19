using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CourseManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class V1AssignmentCompletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Completion",
                table: "CourseAssignmentRecipients",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CompletionMarkedAt",
                table: "CourseAssignmentRecipients",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CompletionMarkedByUserId",
                table: "CourseAssignmentRecipients",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompletionNote",
                table: "CourseAssignmentRecipients",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Completion",
                table: "CourseAssignmentRecipients");

            migrationBuilder.DropColumn(
                name: "CompletionMarkedAt",
                table: "CourseAssignmentRecipients");

            migrationBuilder.DropColumn(
                name: "CompletionMarkedByUserId",
                table: "CourseAssignmentRecipients");

            migrationBuilder.DropColumn(
                name: "CompletionNote",
                table: "CourseAssignmentRecipients");
        }
    }
}
