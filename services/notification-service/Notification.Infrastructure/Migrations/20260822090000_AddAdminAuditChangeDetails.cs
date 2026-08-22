using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Notification.Infrastructure.Persistence;

#nullable disable

namespace Notification.Infrastructure.Migrations;

[DbContext(typeof(NotificationDbContext))]
[Migration("20260822090000_AddAdminAuditChangeDetails")]
public partial class AddAdminAuditChangeDetails : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "Action", table: "AdminAuditRecords", type: "character varying(32)", maxLength: 32, nullable: true);
        migrationBuilder.AddColumn<string>(name: "ResourceType", table: "AdminAuditRecords", type: "character varying(100)", maxLength: 100, nullable: true);
        migrationBuilder.AddColumn<string>(name: "ResourceId", table: "AdminAuditRecords", type: "character varying(100)", maxLength: 100, nullable: true);
        migrationBuilder.AddColumn<string>(name: "ChangedFieldsJson", table: "AdminAuditRecords", type: "character varying(2000)", maxLength: 2000, nullable: true);
        migrationBuilder.CreateIndex(name: "IX_AdminAuditRecords_ResourceType_ResourceId_OccurredAt", table: "AdminAuditRecords", columns: new[] { "ResourceType", "ResourceId", "OccurredAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_AdminAuditRecords_ResourceType_ResourceId_OccurredAt", table: "AdminAuditRecords");
        migrationBuilder.DropColumn(name: "Action", table: "AdminAuditRecords");
        migrationBuilder.DropColumn(name: "ResourceType", table: "AdminAuditRecords");
        migrationBuilder.DropColumn(name: "ResourceId", table: "AdminAuditRecords");
        migrationBuilder.DropColumn(name: "ChangedFieldsJson", table: "AdminAuditRecords");
    }
}
