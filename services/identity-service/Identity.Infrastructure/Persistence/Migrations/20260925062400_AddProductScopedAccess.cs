using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductScopedAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserRoles_UserId",
                schema: "identity",
                table: "UserRoles");

            migrationBuilder.AddColumn<string>(
                name: "Product",
                schema: "identity",
                table: "UserRoles",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UserProductAccess",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Product = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    GrantedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    GrantedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserProductAccess", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserProductAccess_users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_UserId_RoleId_Product",
                schema: "identity",
                table: "UserRoles",
                columns: new[] { "UserId", "RoleId", "Product" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserProductAccess_Product_Status_UserId",
                schema: "identity",
                table: "UserProductAccess",
                columns: new[] { "Product", "Status", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserProductAccess_UserId_Product",
                schema: "identity",
                table: "UserProductAccess",
                columns: new[] { "UserId", "Product" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserProductAccess",
                schema: "identity");

            migrationBuilder.DropIndex(
                name: "IX_UserRoles_UserId_RoleId_Product",
                schema: "identity",
                table: "UserRoles");

            migrationBuilder.DropColumn(
                name: "Product",
                schema: "identity",
                table: "UserRoles");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_UserId",
                schema: "identity",
                table: "UserRoles",
                column: "UserId");
        }
    }
}
