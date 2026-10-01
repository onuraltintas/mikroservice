using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSpeedReadingNewsletterConsentAndCampaignQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "queued_at",
                schema: "speed_reading",
                table: "email_campaigns",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "queued_count",
                schema: "speed_reading",
                table: "email_campaigns",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "newsletter_unsubscribe_token_protected",
                schema: "speed_reading",
                table: "email_campaign_logs",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "queued_at",
                schema: "speed_reading",
                table: "email_campaign_logs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                schema: "speed_reading",
                table: "cms_newsletter_subscribers",
                type: "character varying(320)",
                maxLength: 320,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<DateTime>(
                name: "ConfirmationSentAt",
                schema: "speed_reading",
                table: "cms_newsletter_subscribers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ConfirmationTokenExpiresAt",
                schema: "speed_reading",
                table: "cms_newsletter_subscribers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConfirmationTokenHash",
                schema: "speed_reading",
                table: "cms_newsletter_subscribers",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ConfirmedAt",
                schema: "speed_reading",
                table: "cms_newsletter_subscribers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConsentStatementVersion",
                schema: "speed_reading",
                table: "cms_newsletter_subscribers",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ConsentedAt",
                schema: "speed_reading",
                table: "cms_newsletter_subscribers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PrivacyPolicyVersion",
                schema: "speed_reading",
                table: "cms_newsletter_subscribers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                schema: "speed_reading",
                table: "cms_newsletter_subscribers",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "LegacyUnconfirmed");

            migrationBuilder.AddColumn<string>(
                name: "UnsubscribeTokenHash",
                schema: "speed_reading",
                table: "cms_newsletter_subscribers",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnsubscribeTokenProtected",
                schema: "speed_reading",
                table: "cms_newsletter_subscribers",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UnsubscribedAt",
                schema: "speed_reading",
                table: "cms_newsletter_subscribers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_cms_newsletter_subscribers_ConfirmationTokenHash",
                schema: "speed_reading",
                table: "cms_newsletter_subscribers",
                column: "ConfirmationTokenHash",
                unique: true,
                filter: "\"ConfirmationTokenHash\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_cms_newsletter_subscribers_Status",
                schema: "speed_reading",
                table: "cms_newsletter_subscribers",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_cms_newsletter_subscribers_UnsubscribeTokenHash",
                schema: "speed_reading",
                table: "cms_newsletter_subscribers",
                column: "UnsubscribeTokenHash",
                unique: true,
                filter: "\"UnsubscribeTokenHash\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_cms_newsletter_subscribers_ConfirmationTokenHash",
                schema: "speed_reading",
                table: "cms_newsletter_subscribers");

            migrationBuilder.DropIndex(
                name: "IX_cms_newsletter_subscribers_Status",
                schema: "speed_reading",
                table: "cms_newsletter_subscribers");

            migrationBuilder.DropIndex(
                name: "IX_cms_newsletter_subscribers_UnsubscribeTokenHash",
                schema: "speed_reading",
                table: "cms_newsletter_subscribers");

            migrationBuilder.DropColumn(
                name: "queued_at",
                schema: "speed_reading",
                table: "email_campaigns");

            migrationBuilder.DropColumn(
                name: "queued_count",
                schema: "speed_reading",
                table: "email_campaigns");

            migrationBuilder.DropColumn(
                name: "newsletter_unsubscribe_token_protected",
                schema: "speed_reading",
                table: "email_campaign_logs");

            migrationBuilder.DropColumn(
                name: "queued_at",
                schema: "speed_reading",
                table: "email_campaign_logs");

            migrationBuilder.DropColumn(
                name: "ConfirmationSentAt",
                schema: "speed_reading",
                table: "cms_newsletter_subscribers");

            migrationBuilder.DropColumn(
                name: "ConfirmationTokenExpiresAt",
                schema: "speed_reading",
                table: "cms_newsletter_subscribers");

            migrationBuilder.DropColumn(
                name: "ConfirmationTokenHash",
                schema: "speed_reading",
                table: "cms_newsletter_subscribers");

            migrationBuilder.DropColumn(
                name: "ConfirmedAt",
                schema: "speed_reading",
                table: "cms_newsletter_subscribers");

            migrationBuilder.DropColumn(
                name: "ConsentStatementVersion",
                schema: "speed_reading",
                table: "cms_newsletter_subscribers");

            migrationBuilder.DropColumn(
                name: "ConsentedAt",
                schema: "speed_reading",
                table: "cms_newsletter_subscribers");

            migrationBuilder.DropColumn(
                name: "PrivacyPolicyVersion",
                schema: "speed_reading",
                table: "cms_newsletter_subscribers");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "speed_reading",
                table: "cms_newsletter_subscribers");

            migrationBuilder.DropColumn(
                name: "UnsubscribeTokenHash",
                schema: "speed_reading",
                table: "cms_newsletter_subscribers");

            migrationBuilder.DropColumn(
                name: "UnsubscribeTokenProtected",
                schema: "speed_reading",
                table: "cms_newsletter_subscribers");

            migrationBuilder.DropColumn(
                name: "UnsubscribedAt",
                schema: "speed_reading",
                table: "cms_newsletter_subscribers");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                schema: "speed_reading",
                table: "cms_newsletter_subscribers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(320)",
                oldMaxLength: 320);
        }
    }
}
