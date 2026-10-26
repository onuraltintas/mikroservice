using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class UnifiedTargetCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                LOCK TABLE coaching.target_schools, coaching.target_university_programs,
                    coaching.target_admission_batches, coaching.target_admission_records, coaching.academic_goals
                    IN SHARE ROW EXCLUSIVE MODE;
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM coaching.target_schools s JOIN coaching.target_university_programs p ON s."Id"=p."Id")
                    THEN RAISE EXCEPTION 'Catalog identity collision; review before merging'; END IF;
                END $$;
                CREATE TEMP TABLE unified_schools ON COMMIT DROP AS SELECT * FROM coaching.target_schools;
                CREATE TEMP TABLE unified_goal_links ON COMMIT DROP AS
                    SELECT id, target_school_id, target_university_program_id FROM coaching.academic_goals;
                CREATE TEMP TABLE unified_details ON COMMIT DROP AS
                    SELECT DISTINCT ON (COALESCE(r."ProgramId", r."SchoolId"))
                        COALESCE(r."ProgramId", r."SchoolId") AS target_id,
                        r.*, b."Year" AS source_year, b."Stage" AS source_stage,
                        b."SourceUrl" AS source_url, b."RetrievedAt" AS retrieved_at
                    FROM coaching.target_admission_records r
                    JOIN coaching.target_admission_batches b ON b."Id"=r."BatchId"
                    WHERE r."MatchStatus"='Verified' AND b."PublishedAt" IS NOT NULL
                    ORDER BY COALESCE(r."ProgramId", r."SchoolId"), b."Year" DESC, b."PublishedAt" DESC, r."Id";
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_academic_goals_target_schools_target_school_id",
                schema: "coaching",
                table: "academic_goals");

            migrationBuilder.DropForeignKey(
                name: "FK_academic_goals_target_university_programs_target_university~",
                schema: "coaching",
                table: "academic_goals");

            migrationBuilder.DropTable(
                name: "target_admission_records",
                schema: "coaching");

            migrationBuilder.DropTable(
                name: "target_admission_batches",
                schema: "coaching");

            migrationBuilder.DropTable(
                name: "target_schools",
                schema: "coaching");

            migrationBuilder.DropPrimaryKey(
                name: "PK_target_university_programs",
                schema: "coaching",
                table: "target_university_programs");

            migrationBuilder.DropCheckConstraint(
                name: "ck_target_program_score_type",
                schema: "coaching",
                table: "target_university_programs");

            migrationBuilder.DropCheckConstraint(
                name: "ck_target_program_type",
                schema: "coaching",
                table: "target_university_programs");

            migrationBuilder.RenameTable(
                name: "target_university_programs",
                schema: "coaching",
                newName: "target_catalog",
                newSchema: "coaching");

            migrationBuilder.RenameColumn(
                name: "UniversityName",
                schema: "coaching",
                table: "target_catalog",
                newName: "InstitutionName");

            migrationBuilder.RenameColumn(
                name: "ProgramType",
                schema: "coaching",
                table: "target_catalog",
                newName: "Degree");

            migrationBuilder.RenameIndex(
                name: "IX_target_university_programs_ProgramCode",
                schema: "coaching",
                table: "target_catalog",
                newName: "IX_target_catalog_ProgramCode");

            migrationBuilder.RenameIndex(
                name: "IX_target_university_programs_IsActive_ScoreType",
                schema: "coaching",
                table: "target_catalog",
                newName: "IX_target_catalog_IsActive_ScoreType");

            migrationBuilder.AlterColumn<string>(
                name: "InstitutionName",
                schema: "coaching",
                table: "target_catalog",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(300)",
                oldMaxLength: 300);

            migrationBuilder.AddColumn<string>(
                name: "CatalogType",
                schema: "coaching",
                table: "target_catalog",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "CatalogYear",
                schema: "coaching",
                table: "target_catalog",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "City",
                schema: "coaching",
                table: "target_catalog",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "District",
                schema: "coaching",
                table: "target_catalog",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DistrictId",
                schema: "coaching",
                table: "target_catalog",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EducationType",
                schema: "coaching",
                table: "target_catalog",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Field",
                schema: "coaching",
                table: "target_catalog",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InstitutionType",
                schema: "coaching",
                table: "target_catalog",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Language",
                schema: "coaching",
                table: "target_catalog",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MaximumScore",
                schema: "coaching",
                table: "target_catalog",
                type: "numeric(12,5)",
                precision: 12,
                scale: 5,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinimumScore",
                schema: "coaching",
                table: "target_catalog",
                type: "numeric(12,5)",
                precision: 12,
                scale: 5,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Percentile",
                schema: "coaching",
                table: "target_catalog",
                type: "numeric(7,4)",
                precision: 7,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Placed",
                schema: "coaching",
                table: "target_catalog",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlacementStage",
                schema: "coaching",
                table: "target_catalog",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PlacementYear",
                schema: "coaching",
                table: "target_catalog",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProvinceId",
                schema: "coaching",
                table: "target_catalog",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Quota",
                schema: "coaching",
                table: "target_catalog",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Rank",
                schema: "coaching",
                table: "target_catalog",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Scholarship",
                schema: "coaching",
                table: "target_catalog",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                schema: "coaching",
                table: "target_catalog",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SourceId",
                schema: "coaching",
                table: "target_catalog",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SourceUrl",
                schema: "coaching",
                table: "target_catalog",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                schema: "coaching",
                table: "target_catalog",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE coaching.target_catalog SET "CatalogType"='UniversityProgram',
                    "Source"='mebi-yks', "SourceId"=COALESCE("ProgramCode", "Id"::text);
                INSERT INTO coaching.target_catalog
                    ("Id","CatalogType","Source","SourceId","Name","City","District","ProvinceId","DistrictId","ScoreType",
                     "MinimumScore","PlacementYear","InstitutionType","Language","Field","IsActive","SourceUrl")
                SELECT "Id",'School',"Source","SourceId","Name","City","District","ProvinceId","DistrictId",'LGS',
                    "MinimumScore","ScoreYear","SchoolType","Language","Field","IsActive",
                    CASE WHEN "Source"='mebi-lgs' THEN 'https://ogmmateryal.eba.gov.tr/mebi/lgs-hazirlik/hedef-belirle' END
                FROM unified_schools;
                UPDATE coaching.target_catalog c SET
                    "City"=COALESCE(d."City",c."City"), "District"=COALESCE(d."District",c."District"),
                    "InstitutionType"=COALESCE(d."InstitutionType",c."InstitutionType"),
                    "Language"=COALESCE(d."Language",c."Language"), "Field"=COALESCE(d."Field",c."Field"),
                    "Scholarship"=d."Scholarship", "EducationType"=d."EducationType",
                    "Quota"=d."Quota", "Placed"=d."Placed", "MinimumScore"=COALESCE(d."MinimumScore",c."MinimumScore"),
                    "MaximumScore"=d."MaximumScore", "Rank"=d."Rank", "Percentile"=d."Percentile",
                    "CatalogYear"=d.source_year, "PlacementYear"=CASE WHEN d.source_stage IN ('Main','Transfer1','Transfer2') THEN d.source_year ELSE c."PlacementYear" END,
                    "PlacementStage"=CASE WHEN d.source_stage IN ('Main','Transfer1','Transfer2') THEN d.source_stage END,
                    "SourceUrl"=d.source_url, "UpdatedAt"=d.retrieved_at
                FROM unified_details d WHERE c."Id"=d.target_id AND c."Source"<>'mebi-lgs';
                DO $$ BEGIN
                    IF EXISTS (SELECT * FROM unified_goal_links EXCEPT SELECT id,target_school_id,target_university_program_id FROM coaching.academic_goals)
                    THEN RAISE EXCEPTION 'Goal relationships changed while merging catalog'; END IF;
                    IF (SELECT count(*) FROM coaching.target_catalog WHERE "CatalogType"='School')<>(SELECT count(*) FROM unified_schools)
                    THEN RAISE EXCEPTION 'School row count differs after merging'; END IF;
                END $$;
                """);

            migrationBuilder.AddPrimaryKey(
                name: "PK_target_catalog",
                schema: "coaching",
                table: "target_catalog",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_target_catalog_CatalogType_IsActive_City_District",
                schema: "coaching",
                table: "target_catalog",
                columns: new[] { "CatalogType", "IsActive", "City", "District" });

            migrationBuilder.CreateIndex(
                name: "IX_target_catalog_Source_SourceId",
                schema: "coaching",
                table: "target_catalog",
                columns: new[] { "Source", "SourceId" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_target_catalog_kind",
                schema: "coaching",
                table: "target_catalog",
                sql: "\"CatalogType\" IN ('School','UniversityProgram')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_target_catalog_school",
                schema: "coaching",
                table: "target_catalog",
                sql: "\"CatalogType\" <> 'School' OR (\"City\" IS NOT NULL AND \"District\" IS NOT NULL AND (\"MinimumScore\" IS NULL OR \"MinimumScore\" <= 500) AND (\"MaximumScore\" IS NULL OR \"MaximumScore\" <= 500))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_target_catalog_values",
                schema: "coaching",
                table: "target_catalog",
                sql: "(\"Quota\" IS NULL OR \"Quota\" >= 0) AND (\"Placed\" IS NULL OR \"Placed\" >= 0) AND (\"Rank\" IS NULL OR \"Rank\" > 0) AND (\"Percentile\" IS NULL OR \"Percentile\" BETWEEN 0 AND 100) AND (\"MinimumScore\" IS NULL OR \"MinimumScore\" >= 0) AND (\"MaximumScore\" IS NULL OR \"MaximumScore\" >= 0) AND (\"MinimumScore\" IS NULL OR \"MaximumScore\" IS NULL OR \"MinimumScore\" <= \"MaximumScore\")");

            migrationBuilder.AddCheckConstraint(
                name: "ck_target_catalog_year",
                schema: "coaching",
                table: "target_catalog",
                sql: "(\"CatalogYear\" IS NULL OR \"CatalogYear\" BETWEEN 2026 AND 2100) AND (\"PlacementYear\" IS NULL OR \"PlacementYear\" BETWEEN 1900 AND 2100)");

            migrationBuilder.AddForeignKey(
                name: "FK_academic_goals_target_catalog_target_school_id",
                schema: "coaching",
                table: "academic_goals",
                column: "target_school_id",
                principalSchema: "coaching",
                principalTable: "target_catalog",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_academic_goals_target_catalog_target_university_program_id",
                schema: "coaching",
                table: "academic_goals",
                column: "target_university_program_id",
                principalSchema: "coaching",
                principalTable: "target_catalog",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Restore the verified pre-migration backup to reverse the catalog consolidation.");
        }
    }
}
