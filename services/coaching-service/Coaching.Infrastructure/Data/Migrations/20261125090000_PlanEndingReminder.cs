using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class PlanEndingReminder : Migration
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
                sql: "\"Kind\" IN ('StudyDay', 'StudyOverdue', 'Session', 'FeeOverdue', 'SubscriptionExpiring', 'SubscriptionExpired', 'ParentWeeklySummary', 'AssignmentDue', 'PlanEnding')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The older constraint does not know this kind: its receipts go first (they only say "already reminded").
            migrationBuilder.Sql("DELETE FROM coaching.reminder_receipts WHERE \"Kind\" = 'PlanEnding';");
            migrationBuilder.DropCheckConstraint(
                name: "ck_reminder_kind",
                schema: "coaching",
                table: "reminder_receipts");

            migrationBuilder.AddCheckConstraint(
                name: "ck_reminder_kind",
                schema: "coaching",
                table: "reminder_receipts",
                sql: "\"Kind\" IN ('StudyDay', 'StudyOverdue', 'Session', 'FeeOverdue', 'SubscriptionExpiring', 'SubscriptionExpired', 'ParentWeeklySummary', 'AssignmentDue')");
        }
    }
}
