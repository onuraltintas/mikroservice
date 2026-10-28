using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class NewsletterUnsubscribeLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cms_newsletter_unsubscribe_tokens",
                schema: "coaching",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubscriberId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cms_newsletter_unsubscribe_tokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cms_newsletter_unsubscribe_tokens_cms_newsletter_subscriber~",
                        column: x => x.SubscriberId,
                        principalSchema: "coaching",
                        principalTable: "cms_newsletter_subscribers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cms_newsletter_unsubscribe_tokens_SubscriberId",
                schema: "coaching",
                table: "cms_newsletter_unsubscribe_tokens",
                column: "SubscriberId");

            migrationBuilder.CreateIndex(
                name: "IX_cms_newsletter_unsubscribe_tokens_TokenHash",
                schema: "coaching",
                table: "cms_newsletter_unsubscribe_tokens",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cms_newsletter_unsubscribe_tokens",
                schema: "coaching");
        }
    }
}
