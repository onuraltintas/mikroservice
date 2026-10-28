using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Roadmap O11: the campaign tags (utm_source, utm_medium, utm_campaign) a new account registered with, one row per
    /// account and product, deleted with the account. Down drops the table (registration history only).
    /// </summary>
    public partial class RegistrationSources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RegistrationSources",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Product = table.Column<int>(type: "integer", nullable: false),
                    UtmSource = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UtmMedium = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UtmCampaign = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RegistrationMethod = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistrationSources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RegistrationSources_users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RegistrationSources_Product_CreatedAt",
                schema: "identity",
                table: "RegistrationSources",
                columns: new[] { "Product", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RegistrationSources_UserId_Product",
                schema: "identity",
                table: "RegistrationSources",
                columns: new[] { "UserId", "Product" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "RegistrationSources", schema: "identity");
        }
    }
}
