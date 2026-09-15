using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OwnedSpeedReadingDbContext))]
[Migration("20260915170000_AddBankTransferPayments")]
public partial class AddBankTransferPayments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "bank_transfer_payment_settings",
            schema: "speed_reading",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                AccountHolder = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                BankName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Iban = table.Column<string>(type: "character varying(34)", maxLength: 34, nullable: false),
                Instructions = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table => table.PrimaryKey("pk_bank_transfer_payment_settings", x => x.Id));

        migrationBuilder.CreateTable(
            name: "bank_transfer_payment_requests",
            schema: "speed_reading",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                UserName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                UserEmail = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                Amount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                PaymentReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                PayerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                SubscriptionId = table.Column<Guid>(type: "uuid", nullable: true),
                ReviewedBy = table.Column<Guid>(type: "uuid", nullable: true),
                ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                ReviewNote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_bank_transfer_payment_requests", x => x.Id);
                table.ForeignKey(
                    name: "fk_bank_transfer_payment_requests_subscription_plans_plan_id",
                    column: x => x.PlanId,
                    principalSchema: "speed_reading",
                    principalTable: "subscription_plans",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ix_bank_transfer_payment_requests_user_id_created_at",
            schema: "speed_reading",
            table: "bank_transfer_payment_requests",
            columns: new[] { "UserId", "CreatedAt" });
        migrationBuilder.CreateIndex(
            name: "ix_bank_transfer_payment_requests_status_created_at",
            schema: "speed_reading",
            table: "bank_transfer_payment_requests",
            columns: new[] { "Status", "CreatedAt" });
        migrationBuilder.CreateIndex(
            name: "ix_bank_transfer_payment_requests_user_id_payment_reference",
            schema: "speed_reading",
            table: "bank_transfer_payment_requests",
            columns: new[] { "UserId", "PaymentReference" },
            unique: true);
        migrationBuilder.CreateIndex(
            name: "ix_bank_transfer_payment_requests_subscription_id",
            schema: "speed_reading",
            table: "bank_transfer_payment_requests",
            column: "SubscriptionId",
            unique: true,
            filter: "\"SubscriptionId\" IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "bank_transfer_payment_requests", schema: "speed_reading");
        migrationBuilder.DropTable(name: "bank_transfer_payment_settings", schema: "speed_reading");
    }
}
