using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notification.Infrastructure.Migrations
{
    /// <summary>
    /// Roadmap O9: opt-in SMS / WhatsApp choices (off for everyone), the text-message outbox and commercial-message
    /// consent for İYS. Existing data is unchanged.
    /// </summary>
    public partial class TextMessagingAndCommercialConsent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(name: "Sms", table: "NotificationPreferences", type: "boolean", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<bool>(name: "WhatsApp", table: "NotificationPreferences", type: "boolean", nullable: false, defaultValue: false);

            migrationBuilder.CreateTable(
                name: "CommercialMessageConsents",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Channel = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Granted = table.Column<bool>(type: "boolean", nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Source = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    IysStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_CommercialMessageConsents", x => new { x.UserId, x.Channel }));

            migrationBuilder.CreateTable(
                name: "TextMessageDeliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Channel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ConsumerType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SubjectUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Recipient = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Body = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Provider = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    LastError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table => table.PrimaryKey("PK_TextMessageDeliveries", x => x.Id));

            migrationBuilder.CreateIndex(name: "IX_TextMessageDeliveries_MessageId_Channel", table: "TextMessageDeliveries", columns: new[] { "MessageId", "Channel" }, unique: true);
            migrationBuilder.CreateIndex(name: "IX_TextMessageDeliveries_Status_CreatedAt", table: "TextMessageDeliveries", columns: new[] { "Status", "CreatedAt" });
            migrationBuilder.CreateIndex(name: "IX_TextMessageDeliveries_SubjectUserId", table: "TextMessageDeliveries", column: "SubjectUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "CommercialMessageConsents");
            migrationBuilder.DropTable(name: "TextMessageDeliveries");
            migrationBuilder.DropColumn(name: "Sms", table: "NotificationPreferences");
            migrationBuilder.DropColumn(name: "WhatsApp", table: "NotificationPreferences");
        }
    }
}
