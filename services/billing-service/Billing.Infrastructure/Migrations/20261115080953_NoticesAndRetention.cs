using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Billing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class NoticesAndRetention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "erasure_receipts",
                schema: "billing",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ErasedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RedactAfter = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RedactedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_erasure_receipts", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "notice_receipts",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubscriptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notice_receipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_notice_receipts_subscriptions_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalSchema: "billing",
                        principalTable: "subscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_erasure_receipts_RedactedAt_RedactAfter",
                schema: "billing",
                table: "erasure_receipts",
                columns: new[] { "RedactedAt", "RedactAfter" });

            migrationBuilder.CreateIndex(
                name: "IX_notice_receipts_SubscriptionId_Kind_PeriodEnd",
                schema: "billing",
                table: "notice_receipts",
                columns: new[] { "SubscriptionId", "Kind", "PeriodEnd" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "erasure_receipts",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "notice_receipts",
                schema: "billing");
        }
    }
}
