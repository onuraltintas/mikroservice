using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SupportMultipleSpeedReadingInstitutionRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_institution_memberships_institution_id_user_id",
                schema: "speed_reading",
                table: "institution_memberships");

            migrationBuilder.CreateIndex(
                name: "ux_sra_institution_member_role",
                schema: "speed_reading",
                table: "institution_memberships",
                columns: new[] { "institution_id", "user_id", "role" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_sra_institution_member_role",
                schema: "speed_reading",
                table: "institution_memberships");

            migrationBuilder.CreateIndex(
                name: "IX_institution_memberships_institution_id_user_id",
                schema: "speed_reading",
                table: "institution_memberships",
                columns: new[] { "institution_id", "user_id" },
                unique: true);
        }
    }
}
