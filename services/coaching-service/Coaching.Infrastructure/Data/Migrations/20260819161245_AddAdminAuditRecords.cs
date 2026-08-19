using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminAuditRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdminAuditRecords",
                schema: "coaching",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ServiceName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ActorUserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ActorRoles = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    TenantId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    HttpMethod = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    StatusCode = table.Column<int>(type: "integer", nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ClientIp = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminAuditRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdminAuditRecords_ActorUserId_OccurredAt",
                schema: "coaching",
                table: "AdminAuditRecords",
                columns: new[] { "ActorUserId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AdminAuditRecords_OccurredAt_Id",
                schema: "coaching",
                table: "AdminAuditRecords",
                columns: new[] { "OccurredAt", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminAuditRecords",
                schema: "coaching");
        }
    }
}
