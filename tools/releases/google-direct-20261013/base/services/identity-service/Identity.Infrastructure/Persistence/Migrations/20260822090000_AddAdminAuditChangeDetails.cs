using Microsoft.EntityFrameworkCore.Migrations;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations;

[DbContext(typeof(IdentityDbContext))]
[Migration("20260822090000_AddAdminAuditChangeDetails")]
public partial class AddAdminAuditChangeDetails : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "Action", schema: "identity", table: "AdminAuditRecords", type: "character varying(32)", maxLength: 32, nullable: true);
        migrationBuilder.AddColumn<string>(name: "ResourceType", schema: "identity", table: "AdminAuditRecords", type: "character varying(100)", maxLength: 100, nullable: true);
        migrationBuilder.AddColumn<string>(name: "ResourceId", schema: "identity", table: "AdminAuditRecords", type: "character varying(100)", maxLength: 100, nullable: true);
        migrationBuilder.AddColumn<string>(name: "ChangedFieldsJson", schema: "identity", table: "AdminAuditRecords", type: "character varying(2000)", maxLength: 2000, nullable: true);
        migrationBuilder.CreateIndex(name: "IX_AdminAuditRecords_ResourceType_ResourceId_OccurredAt", schema: "identity", table: "AdminAuditRecords", columns: new[] { "ResourceType", "ResourceId", "OccurredAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_AdminAuditRecords_ResourceType_ResourceId_OccurredAt", schema: "identity", table: "AdminAuditRecords");
        migrationBuilder.DropColumn(name: "Action", schema: "identity", table: "AdminAuditRecords");
        migrationBuilder.DropColumn(name: "ResourceType", schema: "identity", table: "AdminAuditRecords");
        migrationBuilder.DropColumn(name: "ResourceId", schema: "identity", table: "AdminAuditRecords");
        migrationBuilder.DropColumn(name: "ChangedFieldsJson", schema: "identity", table: "AdminAuditRecords");
    }
}
