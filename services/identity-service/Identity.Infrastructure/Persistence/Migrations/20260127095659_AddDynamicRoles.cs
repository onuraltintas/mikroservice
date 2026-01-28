using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDynamicRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_user_roles_users_UserId",
                schema: "identity",
                table: "user_roles");

            migrationBuilder.DropPrimaryKey(
                name: "PK_user_roles",
                schema: "identity",
                table: "user_roles");

            migrationBuilder.DropIndex(
                name: "IX_user_roles_UserId_Role",
                schema: "identity",
                table: "user_roles");

            migrationBuilder.DropColumn(
                name: "Role",
                schema: "identity",
                table: "user_roles");

            migrationBuilder.RenameTable(
                name: "user_roles",
                schema: "identity",
                newName: "UserRoles",
                newSchema: "identity");

            migrationBuilder.AddColumn<Guid>(
                name: "RoleId",
                schema: "identity",
                table: "UserRoles",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserRoles",
                schema: "identity",
                table: "UserRoles",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "Roles",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    IsSystemRole = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_RoleId",
                schema: "identity",
                table: "UserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_UserId",
                schema: "identity",
                table: "UserRoles",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Name",
                schema: "identity",
                table: "Roles",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_UserRoles_Roles_RoleId",
                schema: "identity",
                table: "UserRoles",
                column: "RoleId",
                principalSchema: "identity",
                principalTable: "Roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserRoles_users_UserId",
                schema: "identity",
                table: "UserRoles",
                column: "UserId",
                principalSchema: "identity",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserRoles_Roles_RoleId",
                schema: "identity",
                table: "UserRoles");

            migrationBuilder.DropForeignKey(
                name: "FK_UserRoles_users_UserId",
                schema: "identity",
                table: "UserRoles");

            migrationBuilder.DropTable(
                name: "Roles",
                schema: "identity");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserRoles",
                schema: "identity",
                table: "UserRoles");

            migrationBuilder.DropIndex(
                name: "IX_UserRoles_RoleId",
                schema: "identity",
                table: "UserRoles");

            migrationBuilder.DropIndex(
                name: "IX_UserRoles_UserId",
                schema: "identity",
                table: "UserRoles");

            migrationBuilder.DropColumn(
                name: "RoleId",
                schema: "identity",
                table: "UserRoles");

            migrationBuilder.RenameTable(
                name: "UserRoles",
                schema: "identity",
                newName: "user_roles",
                newSchema: "identity");

            migrationBuilder.AddColumn<string>(
                name: "Role",
                schema: "identity",
                table: "user_roles",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_user_roles",
                schema: "identity",
                table: "user_roles",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_user_roles_UserId_Role",
                schema: "identity",
                table: "user_roles",
                columns: new[] { "UserId", "Role" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_user_roles_users_UserId",
                schema: "identity",
                table: "user_roles",
                column: "UserId",
                principalSchema: "identity",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
