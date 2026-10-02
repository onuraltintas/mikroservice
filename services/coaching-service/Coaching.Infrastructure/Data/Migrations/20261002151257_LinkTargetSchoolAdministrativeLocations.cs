using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class LinkTargetSchoolAdministrativeLocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DistrictId",
                schema: "coaching",
                table: "target_schools",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProvinceId",
                schema: "coaching",
                table: "target_schools",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_target_schools_IsActive_ProvinceId_DistrictId",
                schema: "coaching",
                table: "target_schools",
                columns: new[] { "IsActive", "ProvinceId", "DistrictId" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_target_school_location_pair",
                schema: "coaching",
                table: "target_schools",
                sql: "(\"ProvinceId\" IS NULL AND \"DistrictId\" IS NULL) OR (\"ProvinceId\" IS NOT NULL AND \"DistrictId\" IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM coaching.target_schools WHERE "ProvinceId" IS NOT NULL OR "DistrictId" IS NOT NULL) THEN
                    RAISE EXCEPTION 'Verified school locations exist. Restore a verified backup or explicitly review clearing the mappings before rollback.';
                  END IF;
                END $$;
                """);
            migrationBuilder.DropIndex(
                name: "IX_target_schools_IsActive_ProvinceId_DistrictId",
                schema: "coaching",
                table: "target_schools");

            migrationBuilder.DropCheckConstraint(
                name: "ck_target_school_location_pair",
                schema: "coaching",
                table: "target_schools");

            migrationBuilder.DropColumn(
                name: "DistrictId",
                schema: "coaching",
                table: "target_schools");

            migrationBuilder.DropColumn(
                name: "ProvinceId",
                schema: "coaching",
                table: "target_schools");
        }
    }
}
