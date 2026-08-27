using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OwnedSpeedReadingDbContext))]
[Migration("20260827146000_AddOwnedAdminAudit")]
public partial class AddOwnedAdminAudit : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "admin_audit_records",
            schema: "speed_reading",
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
                UserAgent = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                Action = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                ResourceType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                ResourceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                ChangedFieldsJson = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_admin_audit_records", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "ix_admin_audit_records_occurred_at_id",
            schema: "speed_reading",
            table: "admin_audit_records",
            columns: new[] { "OccurredAt", "Id" });
        migrationBuilder.CreateIndex(
            name: "ix_admin_audit_records_actor_user_id_occurred_at",
            schema: "speed_reading",
            table: "admin_audit_records",
            columns: new[] { "ActorUserId", "OccurredAt" });
        migrationBuilder.CreateIndex(
            name: "ix_admin_audit_records_resource_type_resource_id_occurred_at",
            schema: "speed_reading",
            table: "admin_audit_records",
            columns: new[] { "ResourceType", "ResourceId", "OccurredAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "admin_audit_records",
            schema: "speed_reading");
    }
}
