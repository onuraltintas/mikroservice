using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class OverdueStudySummaries : Migration
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
                sql: "\"Kind\" IN ('StudyDay', 'StudyOverdue', 'Session', 'FeeOverdue')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_reminder_kind",
                schema: "coaching",
                table: "reminder_receipts");

            migrationBuilder.AddCheckConstraint(
                name: "ck_reminder_kind",
                schema: "coaching",
                table: "reminder_receipts",
                sql: "\"Kind\" IN ('StudyDay', 'Session', 'FeeOverdue')");
        }
    }
}
