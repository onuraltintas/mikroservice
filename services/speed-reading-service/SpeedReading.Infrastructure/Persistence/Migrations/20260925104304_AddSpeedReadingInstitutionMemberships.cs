using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSpeedReadingInstitutionMemberships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "institution_memberships",
                schema: "speed_reading",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    institution_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_institution_memberships", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_institution_memberships_institution_id_role_is_active",
                schema: "speed_reading",
                table: "institution_memberships",
                columns: new[] { "institution_id", "role", "is_active" });

            migrationBuilder.CreateIndex(
                name: "IX_institution_memberships_institution_id_user_id",
                schema: "speed_reading",
                table: "institution_memberships",
                columns: new[] { "institution_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_institution_memberships_user_id_is_active",
                schema: "speed_reading",
                table: "institution_memberships",
                columns: new[] { "user_id", "is_active" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "institution_memberships",
                schema: "speed_reading");
        }
    }
}
