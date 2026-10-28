using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Roadmap Y7: a linked parent can send a bank-transfer notice for the child (who sent it is kept), and the weekly
    /// parent summary is recorded as a reminder receipt so it goes out once per week.
    /// </summary>
    public partial class ParentPanel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SubmittedByUserId",
                schema: "coaching",
                table: "bank_transfer_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_bank_transfer_requests_SubmittedByUserId",
                schema: "coaching",
                table: "bank_transfer_requests",
                column: "SubmittedByUserId");

            migrationBuilder.DropCheckConstraint(
                name: "ck_reminder_kind",
                schema: "coaching",
                table: "reminder_receipts");

            migrationBuilder.AddCheckConstraint(
                name: "ck_reminder_kind",
                schema: "coaching",
                table: "reminder_receipts",
                sql: "\"Kind\" IN ('StudyDay', 'StudyOverdue', 'Session', 'FeeOverdue', 'SubscriptionExpiring', 'SubscriptionExpired', 'ParentWeeklySummary')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM coaching.reminder_receipts WHERE \"Kind\" = 'ParentWeeklySummary';");

            migrationBuilder.DropCheckConstraint(
                name: "ck_reminder_kind",
                schema: "coaching",
                table: "reminder_receipts");

            migrationBuilder.AddCheckConstraint(
                name: "ck_reminder_kind",
                schema: "coaching",
                table: "reminder_receipts",
                sql: "\"Kind\" IN ('StudyDay', 'StudyOverdue', 'Session', 'FeeOverdue', 'SubscriptionExpiring', 'SubscriptionExpired')");

            migrationBuilder.DropIndex(
                name: "IX_bank_transfer_requests_SubmittedByUserId",
                schema: "coaching",
                table: "bank_transfer_requests");

            migrationBuilder.DropColumn(
                name: "SubmittedByUserId",
                schema: "coaching",
                table: "bank_transfer_requests");
        }
    }
}
