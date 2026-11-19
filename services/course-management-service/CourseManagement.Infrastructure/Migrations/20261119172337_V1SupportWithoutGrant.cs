using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CourseManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class V1SupportWithoutGrant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "GrantId",
                table: "SupportAccessLogs",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.CreateIndex(
                name: "IX_SupportAccessLogs_WorkspaceId_AdminUserId",
                table: "SupportAccessLogs",
                columns: new[] { "WorkspaceId", "AdminUserId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SupportAccessLogs_WorkspaceId_AdminUserId",
                table: "SupportAccessLogs");

            migrationBuilder.AlterColumn<Guid>(
                name: "GrantId",
                table: "SupportAccessLogs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
