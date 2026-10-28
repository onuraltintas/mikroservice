using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <summary>Roadmap Y1: subscription expiry reminders are de-duplicated through reminder receipts.</summary>
    public partial class SubscriptionLifecycleReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_reminder_kind",
                schema: "coaching",
                table: "reminder_receipts");

            migrationBuilder.AddCheckConstraint(
                name: "ck_reminder_kind",
                schema: "coaching",
                table: "reminder_receipts",
                sql: "\"Kind\" IN ('StudyDay', 'StudyOverdue', 'Session', 'FeeOverdue', 'SubscriptionExpiring', 'SubscriptionExpired')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM coaching.reminder_receipts WHERE \"Kind\" IN ('SubscriptionExpiring', 'SubscriptionExpired');");

            migrationBuilder.DropCheckConstraint(
                name: "ck_reminder_kind",
                schema: "coaching",
                table: "reminder_receipts");

            migrationBuilder.AddCheckConstraint(
                name: "ck_reminder_kind",
                schema: "coaching",
                table: "reminder_receipts",
                sql: "\"Kind\" IN ('StudyDay', 'StudyOverdue', 'Session', 'FeeOverdue')");
        }
    }
}
