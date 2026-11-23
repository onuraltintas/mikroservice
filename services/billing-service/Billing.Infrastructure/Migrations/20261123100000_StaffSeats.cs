using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Billing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class StaffSeats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "StaffSeatsRequired",
                schema: "billing",
                table: "products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                schema: "billing",
                table: "products",
                keyColumn: "Id",
                keyValue: new Guid("b1110000-0000-4000-8000-000000000001"),
                column: "StaffSeatsRequired",
                value: false);

            migrationBuilder.UpdateData(
                schema: "billing",
                table: "products",
                keyColumn: "Id",
                keyValue: new Guid("b1110000-0000-4000-8000-000000000002"),
                column: "StaffSeatsRequired",
                value: false);

            migrationBuilder.UpdateData(
                schema: "billing",
                table: "products",
                keyColumn: "Id",
                keyValue: new Guid("b1110000-0000-4000-8000-000000000003"),
                column: "StaffSeatsRequired",
                value: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StaffSeatsRequired",
                schema: "billing",
                table: "products");
        }
    }
}
