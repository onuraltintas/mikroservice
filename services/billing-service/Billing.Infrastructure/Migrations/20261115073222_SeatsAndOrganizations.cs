using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Billing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeatsAndOrganizations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_orders_one_pending_per_plan",
                schema: "billing",
                table: "orders");

            migrationBuilder.AddColumn<int>(
                name: "ExtraSeats",
                schema: "billing",
                table: "subscriptions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExtraSeatsUntil",
                schema: "billing",
                table: "subscriptions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExtraSeats",
                schema: "billing",
                table: "orders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                schema: "billing",
                table: "orders",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "SubscriptionId",
                schema: "billing",
                table: "orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "seats",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubscriptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsSuspended = table.Column<bool>(type: "boolean", nullable: false),
                    SuspensionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AssignedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_seats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_seats_subscriptions_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalSchema: "billing",
                        principalTable: "subscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_orders_one_pending_per_plan",
                schema: "billing",
                table: "orders",
                columns: new[] { "AccountId", "PlanId", "BeneficiaryUserId", "Kind" },
                unique: true,
                filter: "\"Status\" = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_orders_SubscriptionId",
                schema: "billing",
                table: "orders",
                column: "SubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_seats_SubscriptionId_UserId",
                schema: "billing",
                table: "seats",
                columns: new[] { "SubscriptionId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_seats_UserId",
                schema: "billing",
                table: "seats",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_orders_subscriptions_SubscriptionId",
                schema: "billing",
                table: "orders",
                column: "SubscriptionId",
                principalSchema: "billing",
                principalTable: "subscriptions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_orders_subscriptions_SubscriptionId",
                schema: "billing",
                table: "orders");

            migrationBuilder.DropTable(
                name: "seats",
                schema: "billing");

            migrationBuilder.DropIndex(
                name: "IX_orders_one_pending_per_plan",
                schema: "billing",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "IX_orders_SubscriptionId",
                schema: "billing",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "ExtraSeats",
                schema: "billing",
                table: "subscriptions");

            migrationBuilder.DropColumn(
                name: "ExtraSeatsUntil",
                schema: "billing",
                table: "subscriptions");

            migrationBuilder.DropColumn(
                name: "ExtraSeats",
                schema: "billing",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "Kind",
                schema: "billing",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "SubscriptionId",
                schema: "billing",
                table: "orders");

            migrationBuilder.CreateIndex(
                name: "IX_orders_one_pending_per_plan",
                schema: "billing",
                table: "orders",
                columns: new[] { "AccountId", "PlanId", "BeneficiaryUserId" },
                unique: true,
                filter: "\"Status\" = 'Pending'");
        }
    }
}
