using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Roadmap O10b: a per-seat price on institution plans, extra seats on a license and the institutions' requests for
    /// them. Existing plans sell no extra seats and existing licenses have none. Down refuses while extra seats or
    /// seat requests exist.
    /// </summary>
    public partial class InstitutionExtraSeats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ExtraSeatPrice",
                schema: "coaching",
                table: "subscription_plans",
                type: "numeric(12,2)", precision: 12, scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExtraSeats",
                schema: "coaching",
                table: "subscriptions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "seat_requests",
                schema: "coaching",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubscriptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    Seats = table.Column<int>(type: "integer", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    RemainingDays = table.Column<int>(type: "integer", nullable: false),
                    DurationDays = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    PaymentReference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReviewedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PaymentRecordId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_seat_requests", x => x.Id);
                    table.CheckConstraint("ck_coaching_seat_request_amounts", "\"Seats\" BETWEEN 1 AND 1000 AND \"UnitPrice\" > 0 AND \"Amount\" >= 0 AND \"RemainingDays\" > 0 AND \"DurationDays\" > 0");
                    table.CheckConstraint("ck_coaching_seat_request_status", "\"Status\" IN ('Pending', 'Approved', 'Rejected', 'Withdrawn')");
                    table.ForeignKey(
                        name: "FK_seat_requests_payment_records_PaymentRecordId",
                        column: x => x.PaymentRecordId,
                        principalSchema: "coaching",
                        principalTable: "payment_records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_seat_requests_subscriptions_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalSchema: "coaching",
                        principalTable: "subscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_coaching_subscription_extra_seats",
                schema: "coaching",
                table: "subscriptions",
                sql: "\"ExtraSeats\" >= 0 AND (\"ExtraSeats\" = 0 OR \"InstitutionId\" IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_coaching_subscription_plans_extra_seat",
                schema: "coaching",
                table: "subscription_plans",
                sql: "\"ExtraSeatPrice\" IS NULL OR (\"ExtraSeatPrice\" > 0 AND \"Audience\" = 'Institution')");

            migrationBuilder.CreateIndex(
                name: "IX_seat_requests_InstitutionId",
                schema: "coaching",
                table: "seat_requests",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_seat_requests_PaymentRecordId",
                schema: "coaching",
                table: "seat_requests",
                column: "PaymentRecordId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_seat_requests_Status_CreatedAt",
                schema: "coaching",
                table: "seat_requests",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "ux_coaching_seat_requests_one_pending",
                schema: "coaching",
                table: "seat_requests",
                column: "SubscriptionId",
                unique: true,
                filter: "\"Status\" = 'Pending'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM coaching.seat_requests) OR EXISTS (SELECT 1 FROM coaching.subscriptions WHERE "ExtraSeats" > 0)
                  THEN RAISE EXCEPTION 'Extra seats or seat requests exist; export them before downgrade.'; END IF;
                END $$;
                """);
            migrationBuilder.DropTable(name: "seat_requests", schema: "coaching");
            migrationBuilder.DropCheckConstraint(name: "ck_coaching_subscription_extra_seats", schema: "coaching", table: "subscriptions");
            migrationBuilder.DropCheckConstraint(name: "ck_coaching_subscription_plans_extra_seat", schema: "coaching", table: "subscription_plans");
            migrationBuilder.DropColumn(name: "ExtraSeats", schema: "coaching", table: "subscriptions");
            migrationBuilder.DropColumn(name: "ExtraSeatPrice", schema: "coaching", table: "subscription_plans");
        }
    }
}
