using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class TargetAdmissions2026 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "target_admission_batches",
                schema: "coaching",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExamCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Stage = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SourceUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    DocumentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RetrievedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_target_admission_batches", x => x.Id);
                    table.CheckConstraint("ck_admission_year", "\"Year\" >= 2026");
                });

            migrationBuilder.CreateTable(
                name: "target_admission_records",
                schema: "coaching",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProgramId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExternalCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Institution = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ScoreType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    City = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    District = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    InstitutionType = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Degree = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Language = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Scholarship = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    EducationType = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Field = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Quota = table.Column<int>(type: "integer", nullable: true),
                    Placed = table.Column<int>(type: "integer", nullable: true),
                    MinimumScore = table.Column<decimal>(type: "numeric(12,5)", precision: 12, scale: 5, nullable: true),
                    MaximumScore = table.Column<decimal>(type: "numeric(12,5)", precision: 12, scale: 5, nullable: true),
                    Rank = table.Column<int>(type: "integer", nullable: true),
                    Percentile = table.Column<decimal>(type: "numeric(7,4)", precision: 7, scale: 4, nullable: true),
                    MatchStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Issue = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_target_admission_records", x => x.Id);
                    table.CheckConstraint("ck_admission_target", "NOT (\"SchoolId\" IS NOT NULL AND \"ProgramId\" IS NOT NULL)");
                    table.CheckConstraint("ck_admission_values", "(\"Quota\" IS NULL OR \"Quota\" >= 0) AND (\"Placed\" IS NULL OR \"Placed\" >= 0) AND (\"Rank\" IS NULL OR \"Rank\" > 0) AND (\"Percentile\" IS NULL OR \"Percentile\" BETWEEN 0 AND 100) AND (\"MinimumScore\" IS NULL OR \"MinimumScore\" >= 0) AND (\"MaximumScore\" IS NULL OR \"MaximumScore\" >= 0) AND (\"MinimumScore\" IS NULL OR \"MaximumScore\" IS NULL OR \"MinimumScore\" <= \"MaximumScore\")");
                    table.CheckConstraint("ck_admission_verified", "\"MatchStatus\" IN ('Pending','Ambiguous','Conflict','Verified') AND (\"MatchStatus\" <> 'Verified' OR (\"SchoolId\" IS NOT NULL OR \"ProgramId\" IS NOT NULL))");
                    table.ForeignKey(
                        name: "FK_target_admission_records_target_admission_batches_BatchId",
                        column: x => x.BatchId,
                        principalSchema: "coaching",
                        principalTable: "target_admission_batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_target_admission_records_target_schools_SchoolId",
                        column: x => x.SchoolId,
                        principalSchema: "coaching",
                        principalTable: "target_schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_target_admission_records_target_university_programs_Program~",
                        column: x => x.ProgramId,
                        principalSchema: "coaching",
                        principalTable: "target_university_programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_target_admission_batches_ExamCode_Year_Stage_DocumentHash",
                schema: "coaching",
                table: "target_admission_batches",
                columns: new[] { "ExamCode", "Year", "Stage", "DocumentHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_target_admission_records_BatchId_ExternalCode",
                schema: "coaching",
                table: "target_admission_records",
                columns: new[] { "BatchId", "ExternalCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_target_admission_records_BatchId_MatchStatus_ScoreType",
                schema: "coaching",
                table: "target_admission_records",
                columns: new[] { "BatchId", "MatchStatus", "ScoreType" });

            migrationBuilder.CreateIndex(
                name: "IX_target_admission_records_City_InstitutionType_Degree",
                schema: "coaching",
                table: "target_admission_records",
                columns: new[] { "City", "InstitutionType", "Degree" });

            migrationBuilder.CreateIndex(
                name: "IX_target_admission_records_ProgramId",
                schema: "coaching",
                table: "target_admission_records",
                column: "ProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_target_admission_records_SchoolId",
                schema: "coaching",
                table: "target_admission_records",
                column: "SchoolId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "target_admission_records",
                schema: "coaching");

            migrationBuilder.DropTable(
                name: "target_admission_batches",
                schema: "coaching");
        }
    }
}
