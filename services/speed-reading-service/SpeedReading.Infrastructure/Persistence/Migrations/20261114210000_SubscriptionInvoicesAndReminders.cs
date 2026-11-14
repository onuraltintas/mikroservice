using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace SpeedReading.Infrastructure.Persistence.Migrations;

/// <summary>
/// Hızlı Okuma billing parity (user decision 14.11.2026): invoices of approved bank-transfer payments (the PDF is kept in
/// the database) and one receipt per expiry reminder. Two new tables and two nullable columns; existing rows are untouched.
/// </summary>
[DbContext(typeof(OwnedSpeedReadingDbContext))]
[Migration("20261114210000_SubscriptionInvoicesAndReminders")]
public sealed class SubscriptionInvoicesAndReminders : Migration
{
    private const string Schema = "speed_reading";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "InvoiceNumber", schema: Schema, table: "bank_transfer_payment_requests",
            type: "character varying(40)", maxLength: 40, nullable: true);
        migrationBuilder.AddColumn<DateOnly>(name: "InvoiceIssuedOn", schema: Schema, table: "bank_transfer_payment_requests",
            type: "date", nullable: true);

        migrationBuilder.CreateTable(
            name: "subscription_invoices",
            schema: Schema,
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                PaymentRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                InvoiceNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                IssuedOn = table.Column<DateOnly>(type: "date", nullable: false),
                FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                Content = table.Column<byte[]>(type: "bytea", nullable: false),
                SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_subscription_invoices", item => item.Id));
        migrationBuilder.CreateIndex(name: "IX_subscription_invoices_InvoiceNumber", schema: Schema, table: "subscription_invoices",
            column: "InvoiceNumber", unique: true);
        migrationBuilder.CreateIndex(name: "IX_subscription_invoices_PaymentRequestId", schema: Schema, table: "subscription_invoices",
            column: "PaymentRequestId", unique: true);

        migrationBuilder.CreateTable(
            name: "subscription_notice_receipts",
            schema: Schema,
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                SubscriptionId = table.Column<Guid>(type: "uuid", nullable: false),
                Stage = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_subscription_notice_receipts", item => item.Id));
        migrationBuilder.CreateIndex(name: "IX_subscription_notice_receipts_SubscriptionId_Stage", schema: Schema,
            table: "subscription_notice_receipts", columns: ["SubscriptionId", "Stage"], unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "subscription_notice_receipts", schema: Schema);
        migrationBuilder.DropTable(name: "subscription_invoices", schema: Schema);
        migrationBuilder.DropColumn(name: "InvoiceIssuedOn", schema: Schema, table: "bank_transfer_payment_requests");
        migrationBuilder.DropColumn(name: "InvoiceNumber", schema: Schema, table: "bank_transfer_payment_requests");
    }
}
