using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Roadmap Y5: paid periods of a subscription, the plan length fixed when a transfer notice is sent, and payments
    /// for institution licenses (no paying user). Existing notices take their plan's current length. Subscriptions
    /// approved before this change have no periods; cancelling those keeps the earlier day-count rule.
    /// </summary>
    public partial class SubscriptionPeriodsAndTransferTerms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DurationDays",
                schema: "coaching",
                table: "bank_transfer_requests",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE coaching.bank_transfer_requests AS r SET \"DurationDays\" = p.\"DurationDays\" " +
                "FROM coaching.subscription_plans AS p WHERE p.\"Id\" = r.\"PlanId\";");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "coaching",
                table: "payment_records",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "InstitutionId",
                schema: "coaching",
                table: "payment_records",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_coaching_payment_owner",
                schema: "coaching",
                table: "payment_records",
                sql: "\"UserId\" IS NOT NULL OR \"InstitutionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_payment_records_InstitutionId",
                schema: "coaching",
                table: "payment_records",
                column: "InstitutionId");

            migrationBuilder.CreateTable(
                name: "subscription_periods",
                schema: "coaching",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubscriptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DurationDays = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BankTransferRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subscription_periods", x => x.Id);
                    table.CheckConstraint("ck_coaching_subscription_period_dates", "\"EndDate\" >= \"StartDate\"");
                    table.CheckConstraint("ck_coaching_subscription_period_source", "\"Source\" IN ('BankTransfer', 'Manual')");
                    table.CheckConstraint("ck_coaching_subscription_period_status", "\"Status\" IN ('Active', 'Cancelled')");
                });

            migrationBuilder.CreateIndex(
                name: "IX_subscription_periods_BankTransferRequestId",
                schema: "coaching",
                table: "subscription_periods",
                column: "BankTransferRequestId",
                unique: true,
                filter: "\"BankTransferRequestId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_subscription_periods_SubscriptionId_StartDate",
                schema: "coaching",
                table: "subscription_periods",
                columns: new[] { "SubscriptionId", "StartDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "subscription_periods",
                schema: "coaching");

            migrationBuilder.DropIndex(
                name: "IX_payment_records_InstitutionId",
                schema: "coaching",
                table: "payment_records");

            migrationBuilder.DropCheckConstraint(
                name: "ck_coaching_payment_owner",
                schema: "coaching",
                table: "payment_records");

            // Institution license payments have no paying user and cannot exist in the earlier shape.
            migrationBuilder.Sql("DELETE FROM coaching.payment_records WHERE \"UserId\" IS NULL;");

            migrationBuilder.DropColumn(
                name: "InstitutionId",
                schema: "coaching",
                table: "payment_records");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "coaching",
                table: "payment_records",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "DurationDays",
                schema: "coaching",
                table: "bank_transfer_requests");
        }
    }
}
