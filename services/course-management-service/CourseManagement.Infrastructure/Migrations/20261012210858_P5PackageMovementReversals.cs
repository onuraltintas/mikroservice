using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CourseManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P5PackageMovementReversals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReversalOfMovementId",
                table: "PackageMovements",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PackageMovements_ReversalOfMovementId",
                table: "PackageMovements",
                column: "ReversalOfMovementId",
                unique: true,
                filter: "\"ReversalOfMovementId\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_PackageMovements_PackageMovements_ReversalOfMovementId",
                table: "PackageMovements",
                column: "ReversalOfMovementId",
                principalTable: "PackageMovements",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PackageMovements_PackageMovements_ReversalOfMovementId",
                table: "PackageMovements");

            migrationBuilder.DropIndex(
                name: "IX_PackageMovements_ReversalOfMovementId",
                table: "PackageMovements");

            migrationBuilder.DropColumn(
                name: "ReversalOfMovementId",
                table: "PackageMovements");
        }
    }
}
