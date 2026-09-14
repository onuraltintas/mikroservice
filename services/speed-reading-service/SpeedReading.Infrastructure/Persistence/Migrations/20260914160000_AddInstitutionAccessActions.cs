using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OwnedSpeedReadingDbContext))]
[Migration("20260914160000_AddInstitutionAccessActions")]
public partial class AddInstitutionAccessActions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "institution_access_actions",
            schema: "speed_reading",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                InstitutionAccessLicenseId = table.Column<Guid>(type: "uuid", nullable: false),
                StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                Action = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                PerformedBy = table.Column<Guid>(type: "uuid", nullable: false),
                PerformedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_institution_access_actions", x => x.Id);
                table.ForeignKey("fk_institution_access_actions_institution_access_licenses_institution_access_license_id", x => x.InstitutionAccessLicenseId,
                    principalSchema: "speed_reading", principalTable: "institution_access_licenses", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
            });
        migrationBuilder.CreateIndex("ix_institution_access_actions_license_student_performed_at", "institution_access_actions", new[] { "InstitutionAccessLicenseId", "StudentId", "PerformedAt" }, "speed_reading");
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable("institution_access_actions", "speed_reading");
}
