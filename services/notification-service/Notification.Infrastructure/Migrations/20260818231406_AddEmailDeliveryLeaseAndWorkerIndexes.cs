using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notification.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailDeliveryLeaseAndWorkerIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LeaseToken",
                table: "EmailDeliveries",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailDeliveries_Status_LeaseUntil_CreatedAt",
                table: "EmailDeliveries",
                columns: new[] { "Status", "LeaseUntil", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailDeliveries_Status_NextAttemptAt_CreatedAt",
                table: "EmailDeliveries",
                columns: new[] { "Status", "NextAttemptAt", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EmailDeliveries_Status_LeaseUntil_CreatedAt",
                table: "EmailDeliveries");

            migrationBuilder.DropIndex(
                name: "IX_EmailDeliveries_Status_NextAttemptAt_CreatedAt",
                table: "EmailDeliveries");

            migrationBuilder.DropColumn(
                name: "LeaseToken",
                table: "EmailDeliveries");
        }
    }
}
