using Microsoft.EntityFrameworkCore.Migrations;
using Coaching.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations;

[DbContext(typeof(CoachingDbContext))]
[Migration("20260822090000_AddAdminAuditChangeDetails")]
public partial class AddAdminAuditChangeDetails : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "Action", schema: "coaching", table: "AdminAuditRecords", type: "character varying(32)", maxLength: 32, nullable: true);
        migrationBuilder.AddColumn<string>(name: "ResourceType", schema: "coaching", table: "AdminAuditRecords", type: "character varying(100)", maxLength: 100, nullable: true);
        migrationBuilder.AddColumn<string>(name: "ResourceId", schema: "coaching", table: "AdminAuditRecords", type: "character varying(100)", maxLength: 100, nullable: true);
        migrationBuilder.AddColumn<string>(name: "ChangedFieldsJson", schema: "coaching", table: "AdminAuditRecords", type: "character varying(2000)", maxLength: 2000, nullable: true);
        migrationBuilder.CreateIndex(name: "IX_AdminAuditRecords_ResourceType_ResourceId_OccurredAt", schema: "coaching", table: "AdminAuditRecords", columns: new[] { "ResourceType", "ResourceId", "OccurredAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_AdminAuditRecords_ResourceType_ResourceId_OccurredAt", schema: "coaching", table: "AdminAuditRecords");
        migrationBuilder.DropColumn(name: "Action", schema: "coaching", table: "AdminAuditRecords");
        migrationBuilder.DropColumn(name: "ResourceType", schema: "coaching", table: "AdminAuditRecords");
        migrationBuilder.DropColumn(name: "ResourceId", schema: "coaching", table: "AdminAuditRecords");
        migrationBuilder.DropColumn(name: "ChangedFieldsJson", schema: "coaching", table: "AdminAuditRecords");
    }
}
