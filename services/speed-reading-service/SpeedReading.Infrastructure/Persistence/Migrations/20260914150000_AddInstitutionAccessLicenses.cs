using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OwnedSpeedReadingDbContext))]
[Migration("20260914150000_AddInstitutionAccessLicenses")]
public partial class AddInstitutionAccessLicenses : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "institution_access_licenses",
            schema: "speed_reading",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                SeatCount = table.Column<int>(type: "integer", nullable: false),
                StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                PaymentReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                ApprovedBy = table.Column<Guid>(type: "uuid", nullable: false),
                ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_institution_access_licenses", x => x.Id);
                table.ForeignKey("fk_institution_access_licenses_subscription_plans_plan_id", x => x.PlanId,
                    principalSchema: "speed_reading", principalTable: "subscription_plans", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.AddColumn<Guid>(
            name: "InstitutionAccessLicenseId",
            schema: "speed_reading",
            table: "user_subscriptions",
            type: "uuid",
            nullable: true);
        migrationBuilder.CreateIndex("ix_institution_access_licenses_institution_id_status_end_date", "institution_access_licenses", new[] { "InstitutionId", "Status", "EndDate" }, "speed_reading");
        migrationBuilder.CreateIndex("ix_institution_access_licenses_institution_id_payment_reference", "institution_access_licenses", new[] { "InstitutionId", "PaymentReference" }, "speed_reading", true, "\"PaymentReference\" IS NOT NULL");
        migrationBuilder.CreateIndex("ix_user_subscriptions_institution_access_license_id", "user_subscriptions", "InstitutionAccessLicenseId", "speed_reading");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex("ix_user_subscriptions_institution_access_license_id", "speed_reading", "user_subscriptions");
        migrationBuilder.DropColumn("InstitutionAccessLicenseId", "speed_reading", "user_subscriptions");
        migrationBuilder.DropTable("institution_access_licenses", "speed_reading");
    }
}
