using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OwnedSpeedReadingDbContext))]
[Migration("20260921182500_AddPrivacyErasureExecutions")]
public sealed class AddPrivacyErasureExecutions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "privacy_erasure_executions",
            schema: "speed_reading",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                DeletedRecordCount = table.Column<int>(type: "integer", nullable: false),
                CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_privacy_erasure_executions", item => item.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_privacy_erasure_executions_CompletedAt",
            schema: "speed_reading",
            table: "privacy_erasure_executions",
            column: "CompletedAt");
        migrationBuilder.CreateIndex(
            name: "IX_privacy_erasure_executions_RequestId",
            schema: "speed_reading",
            table: "privacy_erasure_executions",
            column: "RequestId",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "privacy_erasure_executions",
            schema: "speed_reading");
    }
}
