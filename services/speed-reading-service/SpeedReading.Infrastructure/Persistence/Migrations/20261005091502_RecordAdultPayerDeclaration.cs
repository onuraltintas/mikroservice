using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RecordAdultPayerDeclaration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AdultPayerDeclarationVersion",
                schema: "speed_reading",
                table: "bank_transfer_payment_requests",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AdultPayerDeclaredAt",
                schema: "speed_reading",
                table: "bank_transfer_payment_requests",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdultPayerDeclarationVersion",
                schema: "speed_reading",
                table: "bank_transfer_payment_requests");

            migrationBuilder.DropColumn(
                name: "AdultPayerDeclaredAt",
                schema: "speed_reading",
                table: "bank_transfer_payment_requests");
        }
    }
}
