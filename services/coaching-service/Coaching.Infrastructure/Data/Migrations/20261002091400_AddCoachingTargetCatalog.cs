using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCoachingTargetCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "target_schools",
                schema: "coaching",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SourceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    District = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MinimumScore = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: true),
                    ScoreYear = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_target_schools", x => x.Id);
                    table.CheckConstraint("ck_target_school_score", "\"MinimumScore\" IS NULL OR \"MinimumScore\" BETWEEN 0 AND 500");
                });

            migrationBuilder.CreateTable(
                name: "target_university_programs",
                schema: "coaching",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SourceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UniversityName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ProgramCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ScoreType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    MinimumScore = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: true),
                    ScoreYear = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_target_university_programs", x => x.Id);
                    table.CheckConstraint("ck_target_program_score", "\"MinimumScore\" IS NULL OR \"MinimumScore\" >= 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_target_schools_IsActive_City_District",
                schema: "coaching",
                table: "target_schools",
                columns: new[] { "IsActive", "City", "District" });

            migrationBuilder.CreateIndex(
                name: "IX_target_schools_Source_SourceId",
                schema: "coaching",
                table: "target_schools",
                columns: new[] { "Source", "SourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_target_university_programs_IsActive_ScoreType",
                schema: "coaching",
                table: "target_university_programs",
                columns: new[] { "IsActive", "ScoreType" });

            migrationBuilder.CreateIndex(
                name: "IX_target_university_programs_Source_SourceId",
                schema: "coaching",
                table: "target_university_programs",
                columns: new[] { "Source", "SourceId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "target_schools",
                schema: "coaching");

            migrationBuilder.DropTable(
                name: "target_university_programs",
                schema: "coaching");
        }
    }
}
