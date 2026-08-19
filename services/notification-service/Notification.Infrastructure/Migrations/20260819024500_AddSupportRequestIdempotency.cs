using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Notification.Infrastructure.Persistence;

#nullable disable

namespace Notification.Infrastructure.Migrations
{
    [DbContext(typeof(NotificationDbContext))]
    [Migration("20260819024500_AddSupportRequestIdempotency")]
    /// <inheritdoc />
    public partial class AddSupportRequestIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "SupportRequests",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupportRequests_Email_IdempotencyKey",
                table: "SupportRequests",
                columns: new[] { "Email", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SupportRequests_Email_IdempotencyKey",
                table: "SupportRequests");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "SupportRequests");
        }
    }
}
