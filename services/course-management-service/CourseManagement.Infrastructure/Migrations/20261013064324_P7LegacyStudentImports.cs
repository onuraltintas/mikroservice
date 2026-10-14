using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CourseManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P7LegacyStudentImports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LegacyStudentImportMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceSystem = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SourceRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    SelectedFields = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ImportedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegacyStudentImportMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LegacyStudentImportMappings_Students_StudentRecordId",
                        column: x => x.StudentRecordId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LegacyStudentImportMappings_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LegacyStudentImportMappings_StudentRecordId",
                table: "LegacyStudentImportMappings",
                column: "StudentRecordId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LegacyStudentImportMappings_WorkspaceId_SourceSystem_Source~",
                table: "LegacyStudentImportMappings",
                columns: new[] { "WorkspaceId", "SourceSystem", "SourceRecordId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LegacyStudentImportMappings");
        }
    }
}
