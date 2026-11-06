using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// E5 (#22, live test 06.11.2026): a student or parent changes the e-mail address from Profil ve ayarlar › Hesabım.
    /// The new address waits here until the link sent to it is confirmed: the address, a SHA-256 digest of the
    /// one-time link value, its expiry (24 hours) and when the last link was sent (one request a minute). Additive
    /// and nullable; Down drops the four columns (a pending change is lost, the confirmed address is untouched).
    /// </summary>
    public partial class AccountEmailChange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PendingEmail",
                schema: "identity",
                table: "users",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PendingEmailExpiresAt",
                schema: "identity",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PendingEmailRequestedAt",
                schema: "identity",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PendingEmailToken",
                schema: "identity",
                table: "users",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "PendingEmail", schema: "identity", table: "users");
            migrationBuilder.DropColumn(name: "PendingEmailExpiresAt", schema: "identity", table: "users");
            migrationBuilder.DropColumn(name: "PendingEmailRequestedAt", schema: "identity", table: "users");
            migrationBuilder.DropColumn(name: "PendingEmailToken", schema: "identity", table: "users");
        }
    }
}
