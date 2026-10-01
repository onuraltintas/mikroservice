using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRegistrationLegalDocumentAcceptances : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RegistrationLegalDocumentAcceptances",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Product = table.Column<int>(type: "integer", nullable: false),
                    DocumentSlug = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    DocumentVersion = table.Column<int>(type: "integer", nullable: false),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    RegistrationMethod = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AcceptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistrationLegalDocumentAcceptances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RegistrationLegalDocumentAcceptances_users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RegistrationLegalDocumentAcceptances_UserId_Product_Documen~",
                schema: "identity",
                table: "RegistrationLegalDocumentAcceptances",
                columns: new[] { "UserId", "Product", "DocumentSlug", "DocumentVersion" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RegistrationLegalDocumentAcceptances",
                schema: "identity");
        }
    }
}
