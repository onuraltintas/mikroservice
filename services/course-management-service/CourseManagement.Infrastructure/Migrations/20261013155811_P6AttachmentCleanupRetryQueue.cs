using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CourseManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P6AttachmentCleanupRetryQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CourseAssignmentAttachmentCleanupRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    QueuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseAssignmentAttachmentCleanupRequests", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CourseAssignmentAttachmentCleanupRequests_QueuedAt",
                table: "CourseAssignmentAttachmentCleanupRequests",
                column: "QueuedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CourseAssignmentAttachmentCleanupRequests_StorageKey",
                table: "CourseAssignmentAttachmentCleanupRequests",
                column: "StorageKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CourseAssignmentAttachmentCleanupRequests");
        }
    }
}
