using Coaching.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Coaching.Infrastructure.Data.Migrations;

[DbContext(typeof(CoachingDbContext))]
[Migration("20261017215200_AddAssignmentInstructionAttachments")]
public sealed class AddAssignmentInstructionAttachments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "assignment_instruction_attachments",
            schema: "coaching",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                assignment_id = table.Column<Guid>(type: "uuid", nullable: false),
                storage_key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                original_file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                size_bytes = table.Column<long>(type: "bigint", nullable: false),
                sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                uploaded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                scanned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                created_by = table.Column<string>(type: "text", nullable: true),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                updated_by = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_assignment_instruction_attachments", x => x.id);
                table.ForeignKey(
                    name: "FK_assignment_instruction_attachments_assignments_assignment_id",
                    column: x => x.assignment_id,
                    principalSchema: "coaching",
                    principalTable: "assignments",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_assignment_instruction_attachments_assignment",
            schema: "coaching",
            table: "assignment_instruction_attachments",
            column: "assignment_id");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "assignment_instruction_attachments",
            schema: "coaching");
    }
}
