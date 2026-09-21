using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notification.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPrivacyErasureExecutions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PrivacyErasureExecutions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeletedRecordCount = table.Column<int>(type: "integer", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrivacyErasureExecutions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PrivacyErasureExecutions_CompletedAt",
                table: "PrivacyErasureExecutions",
                column: "CompletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PrivacyErasureExecutions_RequestId",
                table: "PrivacyErasureExecutions",
                column: "RequestId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PrivacyErasureExecutions");
        }
    }
}
