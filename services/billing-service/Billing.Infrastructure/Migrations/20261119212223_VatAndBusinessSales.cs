using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Billing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class VatAndBusinessSales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "BusinessSales",
                schema: "billing",
                table: "products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "VatRate",
                schema: "billing",
                table: "plans",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "VatAmount",
                schema: "billing",
                table: "orders",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "VatRate",
                schema: "billing",
                table: "orders",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.UpdateData(
                schema: "billing",
                table: "products",
                keyColumn: "Id",
                keyValue: new Guid("b1110000-0000-4000-8000-000000000001"),
                column: "BusinessSales",
                value: false);

            migrationBuilder.UpdateData(
                schema: "billing",
                table: "products",
                keyColumn: "Id",
                keyValue: new Guid("b1110000-0000-4000-8000-000000000002"),
                column: "BusinessSales",
                value: false);

            migrationBuilder.UpdateData(
                schema: "billing",
                table: "products",
                keyColumn: "Id",
                keyValue: new Guid("b1110000-0000-4000-8000-000000000003"),
                column: "BusinessSales",
                value: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BusinessSales",
                schema: "billing",
                table: "products");

            migrationBuilder.DropColumn(
                name: "VatRate",
                schema: "billing",
                table: "plans");

            migrationBuilder.DropColumn(
                name: "VatAmount",
                schema: "billing",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "VatRate",
                schema: "billing",
                table: "orders");
        }
    }
}
