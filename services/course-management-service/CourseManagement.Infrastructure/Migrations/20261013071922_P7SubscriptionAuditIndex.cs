using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CourseManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P7SubscriptionAuditIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceAuditEntries_SubscriptionAudit",
                table: "WorkspaceAuditEntries",
                columns: new[] { "OccurredAt", "Id" },
                filter: "\"Action\" IN ('Subscription.Assigned', 'Subscription.Updated')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkspaceAuditEntries_SubscriptionAudit",
                table: "WorkspaceAuditEntries");
        }
    }
}
