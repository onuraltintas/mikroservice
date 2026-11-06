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
    /// Review M2: <c>PasswordKnownToOwner</c> (default true). Google registrations used to store a generated password, so
    /// every account with an external sign-in is marked false: Hesabım then offers a password link instead of asking
    /// for a password the owner may never have had. Entering the stored password (sign-in, Hesabım) sets it back.
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

            migrationBuilder.AddColumn<bool>(
                name: "PasswordKnownToOwner",
                schema: "identity",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.Sql("""
                UPDATE identity.users AS u SET "PasswordKnownToOwner" = false
                WHERE EXISTS (SELECT 1 FROM identity."UserLogins" AS l WHERE l."UserId" = u."Id");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "PendingEmail", schema: "identity", table: "users");
            migrationBuilder.DropColumn(name: "PendingEmailExpiresAt", schema: "identity", table: "users");
            migrationBuilder.DropColumn(name: "PendingEmailRequestedAt", schema: "identity", table: "users");
            migrationBuilder.DropColumn(name: "PendingEmailToken", schema: "identity", table: "users");
            migrationBuilder.DropColumn(name: "PasswordKnownToOwner", schema: "identity", table: "users");
        }
    }
}
