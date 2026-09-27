using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ScopeInstitutionAdminUniquenessByProduct : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_institution_admins_UserId_InstitutionId",
                schema: "identity",
                table: "institution_admins");

            migrationBuilder.CreateIndex(
                name: "IX_institution_admins_UserId_InstitutionId_Product",
                schema: "identity",
                table: "institution_admins",
                columns: new[] { "UserId", "InstitutionId", "Product" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_institution_admins_UserId_InstitutionId_Product",
                schema: "identity",
                table: "institution_admins");

            migrationBuilder.CreateIndex(
                name: "IX_institution_admins_UserId_InstitutionId",
                schema: "identity",
                table: "institution_admins",
                columns: new[] { "UserId", "InstitutionId" },
                unique: true);
        }
    }
}
