using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class UnifiedTargetCatalogConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "ck_target_catalog_degree",
                schema: "coaching",
                table: "target_catalog",
                sql: "\"Degree\" IS NULL OR \"Degree\" IN ('Associate','Bachelor')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_target_catalog_location_pair",
                schema: "coaching",
                table: "target_catalog",
                sql: "(\"ProvinceId\" IS NULL AND \"DistrictId\" IS NULL) OR (\"ProvinceId\" IS NOT NULL AND \"DistrictId\" IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_target_catalog_score_type",
                schema: "coaching",
                table: "target_catalog",
                sql: "\"ScoreType\" IS NULL OR (\"CatalogType\" = 'School' AND \"ScoreType\" = 'LGS') OR (\"CatalogType\" = 'UniversityProgram' AND \"ScoreType\" IN ('TYT','SAY','EA','SOZ','YDT'))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_target_catalog_degree",
                schema: "coaching",
                table: "target_catalog");

            migrationBuilder.DropCheckConstraint(
                name: "ck_target_catalog_location_pair",
                schema: "coaching",
                table: "target_catalog");

            migrationBuilder.DropCheckConstraint(
                name: "ck_target_catalog_score_type",
                schema: "coaching",
                table: "target_catalog");
        }
    }
}
