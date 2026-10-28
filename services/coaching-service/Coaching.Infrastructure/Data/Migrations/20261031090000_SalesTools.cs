using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Roadmap O10: per-plan free trial (and trial periods), discount codes, the accepted sales documents and withdrawal
    /// requests on bank transfers, the seller's details, and invoices recorded against payments. Existing rows keep their
    /// values (no trial, no discount); Down refuses to drop trials, coupons, invoices or withdrawal records.
    /// </summary>
    public partial class SalesTools : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CouponCode",
                schema: "coaching",
                table: "bank_transfer_requests",
                type: "character varying(30)", maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                schema: "coaching",
                table: "bank_transfer_requests",
                type: "numeric(12,2)", precision: 12, scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ListPrice",
                schema: "coaching",
                table: "bank_transfer_requests",
                type: "numeric(12,2)", precision: 12, scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SalesContractAcceptedAt",
                schema: "coaching",
                table: "bank_transfer_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SalesContractVersion",
                schema: "coaching",
                table: "bank_transfer_requests",
                type: "character varying(20)", maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SalesDocumentsJson",
                schema: "coaching",
                table: "bank_transfer_requests",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WithdrawalReason",
                schema: "coaching",
                table: "bank_transfer_requests",
                type: "character varying(500)", maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WithdrawalRequestedAt",
                schema: "coaching",
                table: "bank_transfer_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WithdrawalReviewNote",
                schema: "coaching",
                table: "bank_transfer_requests",
                type: "character varying(500)", maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WithdrawalReviewedAt",
                schema: "coaching",
                table: "bank_transfer_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WithdrawalReviewedBy",
                schema: "coaching",
                table: "bank_transfer_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WithdrawalStatus",
                schema: "coaching",
                table: "bank_transfer_requests",
                type: "character varying(20)", maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SellerAddress",
                schema: "coaching",
                table: "subscription_settings",
                type: "character varying(500)", maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SellerEmail",
                schema: "coaching",
                table: "subscription_settings",
                type: "character varying(320)", maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SellerMersisNumber",
                schema: "coaching",
                table: "subscription_settings",
                type: "character varying(20)", maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SellerPhone",
                schema: "coaching",
                table: "subscription_settings",
                type: "character varying(40)", maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SellerTaxNumber",
                schema: "coaching",
                table: "subscription_settings",
                type: "character varying(20)", maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SellerTaxOffice",
                schema: "coaching",
                table: "subscription_settings",
                type: "character varying(100)", maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SellerTitle",
                schema: "coaching",
                table: "subscription_settings",
                type: "character varying(200)", maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TrialDays",
                schema: "coaching",
                table: "subscription_plans",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.DropCheckConstraint(name: "ck_coaching_subscription_period_source", schema: "coaching", table: "subscription_periods");
            migrationBuilder.AddCheckConstraint(name: "ck_coaching_subscription_period_source", schema: "coaching", table: "subscription_periods",
                sql: "\"Source\" IN ('BankTransfer', 'Manual', 'Trial')");
            migrationBuilder.AddCheckConstraint(name: "ck_coaching_subscription_plans_trial", schema: "coaching", table: "subscription_plans",
                sql: "\"TrialDays\" BETWEEN 0 AND 90 AND (\"TrialDays\" = 0 OR \"Audience\" <> 'Institution')");
            migrationBuilder.AddCheckConstraint(name: "ck_coaching_bank_transfer_discount", schema: "coaching", table: "bank_transfer_requests",
                sql: "\"DiscountAmount\" >= 0 AND (\"CouponCode\" IS NOT NULL OR \"DiscountAmount\" = 0)");
            migrationBuilder.AddCheckConstraint(name: "ck_coaching_bank_transfer_withdrawal", schema: "coaching", table: "bank_transfer_requests",
                sql: "\"WithdrawalStatus\" IS NULL OR \"WithdrawalStatus\" IN ('Requested', 'Approved', 'Rejected')");

            migrationBuilder.CreateTable(
                name: "coupons",
                schema: "coaching",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Kind = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Value = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    ValidFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ValidUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MaxUses = table.Column<int>(type: "integer", nullable: true),
                    UsedCount = table.Column<int>(type: "integer", nullable: false),
                    PlanIdsJson = table.Column<string>(type: "jsonb", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_coupons", x => x.Id);
                    table.CheckConstraint("ck_coaching_coupon_kind", "(\"Kind\" = 'Percent' AND \"Value\" > 0 AND \"Value\" <= 100) OR (\"Kind\" = 'Amount' AND \"Value\" > 0)");
                    table.CheckConstraint("ck_coaching_coupon_uses", "\"UsedCount\" >= 0 AND (\"MaxUses\" IS NULL OR \"MaxUses\" > 0)");
                });

            migrationBuilder.CreateTable(
                name: "invoices",
                schema: "coaching",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    IssuedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_invoices_payment_records_PaymentRecordId",
                        column: x => x.PaymentRecordId,
                        principalSchema: "coaching",
                        principalTable: "payment_records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "subscription_trials",
                schema: "coaching",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Audience = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubscriptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subscription_trials", x => x.Id);
                    table.CheckConstraint("ck_coaching_subscription_trial_audience", "\"Audience\" IN ('Individual', 'Teacher')");
                });

            migrationBuilder.CreateIndex(name: "IX_coupons_Code", schema: "coaching", table: "coupons", column: "Code", unique: true);
            migrationBuilder.CreateIndex(name: "IX_invoices_InvoiceNumber", schema: "coaching", table: "invoices", column: "InvoiceNumber", unique: true);
            migrationBuilder.CreateIndex(name: "IX_invoices_PaymentRecordId", schema: "coaching", table: "invoices", column: "PaymentRecordId", unique: true);
            migrationBuilder.CreateIndex(name: "IX_invoices_StorageKey", schema: "coaching", table: "invoices", column: "StorageKey", unique: true);
            migrationBuilder.CreateIndex(name: "IX_subscription_trials_UserId_Audience", schema: "coaching", table: "subscription_trials", columns: new[] { "UserId", "Audience" }, unique: true);
            migrationBuilder.CreateIndex(name: "IX_payment_records_UserId", schema: "coaching", table: "payment_records", column: "UserId");
            migrationBuilder.CreateIndex(name: "IX_bank_transfer_requests_UserId_CouponCode", schema: "coaching", table: "bank_transfer_requests",
                columns: new[] { "UserId", "CouponCode" }, filter: "\"CouponCode\" IS NOT NULL");
            migrationBuilder.CreateIndex(name: "IX_bank_transfer_requests_WithdrawalStatus_WithdrawalRequestedAt", schema: "coaching", table: "bank_transfer_requests",
                columns: new[] { "WithdrawalStatus", "WithdrawalRequestedAt" }, filter: "\"WithdrawalStatus\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM coaching.subscription_trials) OR EXISTS (SELECT 1 FROM coaching.coupons)
                    OR EXISTS (SELECT 1 FROM coaching.invoices) OR EXISTS (SELECT 1 FROM coaching.subscription_periods WHERE "Source" = 'Trial')
                    OR EXISTS (SELECT 1 FROM coaching.bank_transfer_requests WHERE "CouponCode" IS NOT NULL OR "WithdrawalStatus" IS NOT NULL OR "SalesDocumentsJson" IS NOT NULL)
                  THEN RAISE EXCEPTION 'Trials, coupons, invoices, accepted sales documents or withdrawals exist; export them before downgrade.'; END IF;
                END $$;
                """);
            migrationBuilder.DropTable(name: "coupons", schema: "coaching");
            migrationBuilder.DropTable(name: "invoices", schema: "coaching");
            migrationBuilder.DropTable(name: "subscription_trials", schema: "coaching");
            migrationBuilder.DropIndex(name: "IX_payment_records_UserId", schema: "coaching", table: "payment_records");
            migrationBuilder.DropIndex(name: "IX_bank_transfer_requests_UserId_CouponCode", schema: "coaching", table: "bank_transfer_requests");
            migrationBuilder.DropIndex(name: "IX_bank_transfer_requests_WithdrawalStatus_WithdrawalRequestedAt", schema: "coaching", table: "bank_transfer_requests");
            migrationBuilder.DropCheckConstraint(name: "ck_coaching_bank_transfer_discount", schema: "coaching", table: "bank_transfer_requests");
            migrationBuilder.DropCheckConstraint(name: "ck_coaching_bank_transfer_withdrawal", schema: "coaching", table: "bank_transfer_requests");
            migrationBuilder.DropCheckConstraint(name: "ck_coaching_subscription_plans_trial", schema: "coaching", table: "subscription_plans");
            migrationBuilder.DropCheckConstraint(name: "ck_coaching_subscription_period_source", schema: "coaching", table: "subscription_periods");
            migrationBuilder.AddCheckConstraint(name: "ck_coaching_subscription_period_source", schema: "coaching", table: "subscription_periods",
                sql: "\"Source\" IN ('BankTransfer', 'Manual')");
            migrationBuilder.DropColumn(name: "CouponCode", schema: "coaching", table: "bank_transfer_requests");
            migrationBuilder.DropColumn(name: "DiscountAmount", schema: "coaching", table: "bank_transfer_requests");
            migrationBuilder.DropColumn(name: "ListPrice", schema: "coaching", table: "bank_transfer_requests");
            migrationBuilder.DropColumn(name: "SalesContractAcceptedAt", schema: "coaching", table: "bank_transfer_requests");
            migrationBuilder.DropColumn(name: "SalesContractVersion", schema: "coaching", table: "bank_transfer_requests");
            migrationBuilder.DropColumn(name: "SalesDocumentsJson", schema: "coaching", table: "bank_transfer_requests");
            migrationBuilder.DropColumn(name: "WithdrawalReason", schema: "coaching", table: "bank_transfer_requests");
            migrationBuilder.DropColumn(name: "WithdrawalRequestedAt", schema: "coaching", table: "bank_transfer_requests");
            migrationBuilder.DropColumn(name: "WithdrawalReviewNote", schema: "coaching", table: "bank_transfer_requests");
            migrationBuilder.DropColumn(name: "WithdrawalReviewedAt", schema: "coaching", table: "bank_transfer_requests");
            migrationBuilder.DropColumn(name: "WithdrawalReviewedBy", schema: "coaching", table: "bank_transfer_requests");
            migrationBuilder.DropColumn(name: "WithdrawalStatus", schema: "coaching", table: "bank_transfer_requests");
            migrationBuilder.DropColumn(name: "SellerAddress", schema: "coaching", table: "subscription_settings");
            migrationBuilder.DropColumn(name: "SellerEmail", schema: "coaching", table: "subscription_settings");
            migrationBuilder.DropColumn(name: "SellerMersisNumber", schema: "coaching", table: "subscription_settings");
            migrationBuilder.DropColumn(name: "SellerPhone", schema: "coaching", table: "subscription_settings");
            migrationBuilder.DropColumn(name: "SellerTaxNumber", schema: "coaching", table: "subscription_settings");
            migrationBuilder.DropColumn(name: "SellerTaxOffice", schema: "coaching", table: "subscription_settings");
            migrationBuilder.DropColumn(name: "SellerTitle", schema: "coaching", table: "subscription_settings");
            migrationBuilder.DropColumn(name: "TrialDays", schema: "coaching", table: "subscription_plans");
        }
    }
}
