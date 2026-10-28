using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <summary>Roadmap O8: reminder receipts may also record the day-before assignment reminder. Down refuses while such receipts exist.</summary>
    public partial class AssignmentDueReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(name: "ck_reminder_kind", schema: "coaching", table: "reminder_receipts");
            migrationBuilder.AddCheckConstraint(
                name: "ck_reminder_kind",
                schema: "coaching",
                table: "reminder_receipts",
                sql: "\"Kind\" IN ('StudyDay', 'StudyOverdue', 'Session', 'FeeOverdue', 'SubscriptionExpiring', 'SubscriptionExpired', 'ParentWeeklySummary', 'AssignmentDue')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM coaching.reminder_receipts WHERE "Kind" = 'AssignmentDue') THEN
                        RAISE EXCEPTION 'AssignmentDue reminder receipts exist; remove them deliberately before downgrading.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropCheckConstraint(name: "ck_reminder_kind", schema: "coaching", table: "reminder_receipts");
            migrationBuilder.AddCheckConstraint(
                name: "ck_reminder_kind",
                schema: "coaching",
                table: "reminder_receipts",
                sql: "\"Kind\" IN ('StudyDay', 'StudyOverdue', 'Session', 'FeeOverdue', 'SubscriptionExpiring', 'SubscriptionExpired', 'ParentWeeklySummary')");
        }
    }
}
