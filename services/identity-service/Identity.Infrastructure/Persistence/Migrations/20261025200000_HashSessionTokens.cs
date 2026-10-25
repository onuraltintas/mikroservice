using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Stores refresh and password-reset tokens as SHA-256 digests, groups refresh tokens
    /// into sign-in families (reuse detection, absolute session lifetime) and indexes the
    /// token lookup. Existing values are hashed in place, so signed-in users stay signed in
    /// and pending reset links keep working.
    /// </summary>
    public partial class HashSessionTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FamilyId",
                schema: "identity",
                table: "RefreshTokens",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "SessionStartedAt",
                schema: "identity",
                table: "RefreshTokens",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            // Each existing token starts its own family at the time it was issued.
            // Digest = lower-case hex SHA-256 of the UTF-8 value (RefreshToken.Digest).
            migrationBuilder.Sql("""
                UPDATE identity."RefreshTokens"
                SET "FamilyId" = "Id",
                    "SessionStartedAt" = "CreatedAt",
                    "Token" = encode(sha256(convert_to("Token", 'UTF8')), 'hex');
                """);

            migrationBuilder.Sql("""
                UPDATE identity.users
                SET "PasswordResetToken" = encode(sha256(convert_to("PasswordResetToken", 'UTF8')), 'hex')
                WHERE "PasswordResetToken" IS NOT NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_ExpiresAt",
                schema: "identity",
                table: "RefreshTokens",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_FamilyId",
                schema: "identity",
                table: "RefreshTokens",
                column: "FamilyId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_Token",
                schema: "identity",
                table: "RefreshTokens",
                column: "Token",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_ExpiresAt",
                schema: "identity",
                table: "RefreshTokens");

            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_FamilyId",
                schema: "identity",
                table: "RefreshTokens");

            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_Token",
                schema: "identity",
                table: "RefreshTokens");

            // Digests cannot be turned back into values: the previous code would never
            // match them, so end the sessions and links explicitly instead.
            migrationBuilder.Sql("""
                UPDATE identity."RefreshTokens"
                SET "RevokedAt" = now(), "RevokedByIp" = 'system', "ReasonRevoked" = 'token storage downgrade'
                WHERE "RevokedAt" IS NULL;
                UPDATE identity.users
                SET "PasswordResetToken" = NULL, "PasswordResetTokenExpiresAt" = NULL
                WHERE "PasswordResetToken" IS NOT NULL;
                """);

            migrationBuilder.DropColumn(
                name: "FamilyId",
                schema: "identity",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "SessionStartedAt",
                schema: "identity",
                table: "RefreshTokens");
        }
    }
}
