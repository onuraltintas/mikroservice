using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OwnedSpeedReadingDbContext))]
[Migration("20260827141000_AddOwnedWriteSupport")]
public partial class AddOwnedWriteSupport : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "Description",
            schema: "speed_reading",
            table: "program_templates",
            type: "character varying(5000)",
            maxLength: 5000,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(4000)",
            oldMaxLength: 4000);
        migrationBuilder.AddColumn<bool>(
            name: "IsDeleted",
            schema: "speed_reading",
            table: "program_templates",
            type: "boolean",
            nullable: false,
            defaultValue: false);
        migrationBuilder.AddColumn<DateTime>(
            name: "DeletedAt",
            schema: "speed_reading",
            table: "program_templates",
            type: "timestamp with time zone",
            nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "DeletedBy",
            schema: "speed_reading",
            table: "program_templates",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.CreateTable(
            name: "idempotency_records",
            schema: "speed_reading",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                Scope = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                RequestHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_idempotency_records", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "ix_idempotency_records_created_at",
            schema: "speed_reading",
            table: "idempotency_records",
            column: "created_at");
        migrationBuilder.CreateIndex(
            name: "ix_idempotency_records_scope_key",
            schema: "speed_reading",
            table: "idempotency_records",
            columns: new[] { "Scope", "Key" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "idempotency_records", schema: "speed_reading");
        migrationBuilder.DropColumn(name: "IsDeleted", schema: "speed_reading", table: "program_templates");
        migrationBuilder.DropColumn(name: "DeletedAt", schema: "speed_reading", table: "program_templates");
        migrationBuilder.DropColumn(name: "DeletedBy", schema: "speed_reading", table: "program_templates");
        migrationBuilder.AlterColumn<string>(
            name: "Description",
            schema: "speed_reading",
            table: "program_templates",
            type: "character varying(4000)",
            maxLength: 4000,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(5000)",
            oldMaxLength: 5000);
    }
}
