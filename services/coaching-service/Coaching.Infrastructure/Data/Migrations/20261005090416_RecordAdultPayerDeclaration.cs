using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RecordAdultPayerDeclaration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AdultPayerDeclarationVersion",
                schema: "coaching",
                table: "bank_transfer_requests",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AdultPayerDeclaredAt",
                schema: "coaching",
                table: "bank_transfer_requests",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdultPayerDeclarationVersion",
                schema: "coaching",
                table: "bank_transfer_requests");

            migrationBuilder.DropColumn(
                name: "AdultPayerDeclaredAt",
                schema: "coaching",
                table: "bank_transfer_requests");
        }
    }
}
