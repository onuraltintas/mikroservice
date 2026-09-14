using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTurkeyAdministrativeLocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DistrictId",
                schema: "identity",
                table: "institutions",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProvinceId",
                schema: "identity",
                table: "institutions",
                type: "character varying(12)",
                maxLength: 12,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "provinces",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_provinces", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "districts",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ProvinceId = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_districts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_districts_provinces_ProvinceId",
                        column: x => x.ProvinceId,
                        principalSchema: "identity",
                        principalTable: "provinces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_institutions_DistrictId",
                schema: "identity",
                table: "institutions",
                column: "DistrictId");

            migrationBuilder.CreateIndex(
                name: "IX_institutions_ProvinceId",
                schema: "identity",
                table: "institutions",
                column: "ProvinceId");

            migrationBuilder.CreateIndex(
                name: "IX_districts_ProvinceId_Name",
                schema: "identity",
                table: "districts",
                columns: new[] { "ProvinceId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_provinces_Name",
                schema: "identity",
                table: "provinces",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_institutions_districts_DistrictId",
                schema: "identity",
                table: "institutions",
                column: "DistrictId",
                principalSchema: "identity",
                principalTable: "districts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_institutions_provinces_ProvinceId",
                schema: "identity",
                table: "institutions",
                column: "ProvinceId",
                principalSchema: "identity",
                principalTable: "provinces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_institutions_districts_DistrictId",
                schema: "identity",
                table: "institutions");

            migrationBuilder.DropForeignKey(
                name: "FK_institutions_provinces_ProvinceId",
                schema: "identity",
                table: "institutions");

            migrationBuilder.DropTable(
                name: "districts",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "provinces",
                schema: "identity");

            migrationBuilder.DropIndex(
                name: "IX_institutions_DistrictId",
                schema: "identity",
                table: "institutions");

            migrationBuilder.DropIndex(
                name: "IX_institutions_ProvinceId",
                schema: "identity",
                table: "institutions");

            migrationBuilder.DropColumn(
                name: "DistrictId",
                schema: "identity",
                table: "institutions");

            migrationBuilder.DropColumn(
                name: "ProvinceId",
                schema: "identity",
                table: "institutions");
        }
    }
}
