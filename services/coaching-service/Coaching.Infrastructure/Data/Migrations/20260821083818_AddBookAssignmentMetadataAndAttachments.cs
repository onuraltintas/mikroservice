using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBookAssignmentMetadataAndAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "book_chapter",
                schema: "coaching",
                table: "assignments",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "book_edition",
                schema: "coaching",
                table: "assignments",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "book_end_page",
                schema: "coaching",
                table: "assignments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "book_end_question",
                schema: "coaching",
                table: "assignments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "book_isbn",
                schema: "coaching",
                table: "assignments",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "book_start_page",
                schema: "coaching",
                table: "assignments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "book_start_question",
                schema: "coaching",
                table: "assignments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "book_title",
                schema: "coaching",
                table: "assignments",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source",
                schema: "coaching",
                table: "assignments",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Digital");

            migrationBuilder.CreateTable(
                name: "assignment_submission_attachments",
                schema: "coaching",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    assignment_student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    storage_key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    original_file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    upload_expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    uploaded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    scanned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rejection_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assignment_submission_attachments", x => x.id);
                    table.ForeignKey(
                        name: "FK_assignment_submission_attachments_assignment_students_assig~",
                        column: x => x.assignment_student_id,
                        principalSchema: "coaching",
                        principalTable: "assignment_students",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_assignment_submission_attachments_status",
                schema: "coaching",
                table: "assignment_submission_attachments",
                columns: new[] { "assignment_student_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_assignment_submission_attachments_student",
                schema: "coaching",
                table: "assignment_submission_attachments",
                column: "assignment_student_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "assignment_submission_attachments",
                schema: "coaching");

            migrationBuilder.DropColumn(
                name: "book_chapter",
                schema: "coaching",
                table: "assignments");

            migrationBuilder.DropColumn(
                name: "book_edition",
                schema: "coaching",
                table: "assignments");

            migrationBuilder.DropColumn(
                name: "book_end_page",
                schema: "coaching",
                table: "assignments");

            migrationBuilder.DropColumn(
                name: "book_end_question",
                schema: "coaching",
                table: "assignments");

            migrationBuilder.DropColumn(
                name: "book_isbn",
                schema: "coaching",
                table: "assignments");

            migrationBuilder.DropColumn(
                name: "book_start_page",
                schema: "coaching",
                table: "assignments");

            migrationBuilder.DropColumn(
                name: "book_start_question",
                schema: "coaching",
                table: "assignments");

            migrationBuilder.DropColumn(
                name: "book_title",
                schema: "coaching",
                table: "assignments");

            migrationBuilder.DropColumn(
                name: "source",
                schema: "coaching",
                table: "assignments");
        }
    }
}
