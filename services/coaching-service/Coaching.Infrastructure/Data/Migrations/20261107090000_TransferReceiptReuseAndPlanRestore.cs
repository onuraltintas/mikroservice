using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Coaching audit K1 (31.10.2026): a bank receipt number is unique per payer only among notices still waiting or
    /// approved, so a withdrawn, rejected or expired notice no longer blocks sending the same receipt again; a paid period
    /// remembers the plan an upgrade replaced. Down refuses while two notices of one payer share a receipt number.
    /// </summary>
    public partial class TransferReceiptReuseAndPlanRestore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_bank_transfer_requests_UserId_PaymentReference",
                schema: "coaching",
                table: "bank_transfer_requests");

            migrationBuilder.CreateIndex(
                name: "IX_bank_transfer_requests_UserId_PaymentReference",
                schema: "coaching",
                table: "bank_transfer_requests",
                columns: new[] { "UserId", "PaymentReference" },
                unique: true,
                filter: "\"Status\" IN ('Pending', 'Approved')");

            migrationBuilder.AddColumn<Guid>(
                name: "PreviousPlanId",
                schema: "coaching",
                table: "subscription_periods",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM coaching.bank_transfer_requests
                               GROUP BY "UserId", "PaymentReference" HAVING count(*) > 1) THEN
                        RAISE EXCEPTION 'A payer has sent the same receipt number more than once; resolve those notices before downgrading.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropColumn(
                name: "PreviousPlanId",
                schema: "coaching",
                table: "subscription_periods");

            migrationBuilder.DropIndex(
                name: "IX_bank_transfer_requests_UserId_PaymentReference",
                schema: "coaching",
                table: "bank_transfer_requests");

            migrationBuilder.CreateIndex(
                name: "IX_bank_transfer_requests_UserId_PaymentReference",
                schema: "coaching",
                table: "bank_transfer_requests",
                columns: new[] { "UserId", "PaymentReference" },
                unique: true);
        }
    }
}
