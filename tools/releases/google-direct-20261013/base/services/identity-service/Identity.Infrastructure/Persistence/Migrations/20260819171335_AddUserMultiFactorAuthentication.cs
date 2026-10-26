using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserMultiFactorAuthentication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "LastAcceptedMfaTimeStep",
                schema: "identity",
                table: "users",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "MfaEnabled",
                schema: "identity",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "MfaEnabledAt",
                schema: "identity",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MfaFailedAttempts",
                schema: "identity",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "MfaLockedUntil",
                schema: "identity",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MfaRecoveryCodeHashesJson",
                schema: "identity",
                table: "users",
                type: "character varying(4096)",
                maxLength: 4096,
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "MfaSecretProtected",
                schema: "identity",
                table: "users",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastAcceptedMfaTimeStep",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "MfaEnabled",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "MfaEnabledAt",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "MfaFailedAttempts",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "MfaLockedUntil",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "MfaRecoveryCodeHashesJson",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "MfaSecretProtected",
                schema: "identity",
                table: "users");
        }
    }
}
