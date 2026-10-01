using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCoachingNewsletterSubscribers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cms_newsletter_subscribers",
                schema: "coaching",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Source = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ConsentTextVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ConsentedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConfirmedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UnsubscribedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConfirmationTokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ConfirmationTokenExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UnsubscribeTokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cms_newsletter_subscribers", x => x.Id);
                    table.CheckConstraint("ck_coaching_newsletter_subscriber_status", "\"Status\" IN ('PendingConfirmation', 'Active', 'Unsubscribed')");
                });

            migrationBuilder.CreateIndex(
                name: "IX_cms_newsletter_subscribers_ConfirmationTokenHash",
                schema: "coaching",
                table: "cms_newsletter_subscribers",
                column: "ConfirmationTokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cms_newsletter_subscribers_Email",
                schema: "coaching",
                table: "cms_newsletter_subscribers",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cms_newsletter_subscribers_Status",
                schema: "coaching",
                table: "cms_newsletter_subscribers",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_cms_newsletter_subscribers_UnsubscribeTokenHash",
                schema: "coaching",
                table: "cms_newsletter_subscribers",
                column: "UnsubscribeTokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cms_newsletter_subscribers",
                schema: "coaching");
        }
    }
}
