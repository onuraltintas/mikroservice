using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class DynamicCurriculumExams : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_outcome_edition_scope",
                schema: "coaching",
                table: "official_curriculum_editions");

            migrationBuilder.CreateTable(
                name: "study_catalog_exams",
                schema: "coaching",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_study_catalog_exams", x => x.Id);
                    table.UniqueConstraint("AK_study_catalog_exams_Code", x => x.Code);
                    table.CheckConstraint("ck_catalog_exam_order", "\"DisplayOrder\" >= 0");
                });

            migrationBuilder.InsertData(
                schema: "coaching",
                table: "study_catalog_exams",
                columns: new[] { "Id", "Code", "DisplayOrder", "IsActive", "Name" },
                values: new object[,]
                {
                    { new Guid("f0ca1000-0000-4000-8000-000000000001"), "LGS", 0, true, "Liselere Geçiş Sistemi" },
                    { new Guid("f0ca1000-0000-4000-8000-000000000002"), "TYT", 1, true, "Temel Yeterlilik Testi" },
                    { new Guid("f0ca1000-0000-4000-8000-000000000003"), "AYT", 2, true, "Alan Yeterlilik Testleri" },
                    { new Guid("f0ca1000-0000-4000-8000-000000000004"), "YDT", 3, true, "Yabancı Dil Testi" },
                    { new Guid("f0ca1000-0000-4000-8000-000000000005"), "TDP", 4, true, "Temel Destek Paketleri" }
                });

            migrationBuilder.Sql("""
                INSERT INTO coaching.study_catalog_exams ("Id", "Code", "Name", "DisplayOrder", "IsActive")
                SELECT md5('coaching-exam:' || codes.code)::uuid, codes.code, codes.code, 100, true
                FROM (SELECT "ExamCode" AS code FROM coaching.study_catalog_lessons WHERE "ExamCode" IS NOT NULL
                      UNION SELECT "ExamCode" FROM coaching.official_curriculum_editions) codes
                WHERE NOT EXISTS (SELECT 1 FROM coaching.study_catalog_exams e WHERE e."Code" = codes.code);
                """);

            migrationBuilder.CreateIndex(
                name: "IX_study_catalog_lessons_ExamCode",
                schema: "coaching",
                table: "study_catalog_lessons",
                column: "ExamCode");

            migrationBuilder.CreateIndex(
                name: "IX_official_curriculum_editions_ExamCode",
                schema: "coaching",
                table: "official_curriculum_editions",
                column: "ExamCode");

            migrationBuilder.AddCheckConstraint(
                name: "ck_outcome_edition_scope",
                schema: "coaching",
                table: "official_curriculum_editions",
                sql: "\"GradeNumber\" BETWEEN 1 AND 12 AND \"AcademicYearStart\" BETWEEN 2000 AND 2200 AND \"AcademicYearEnd\" BETWEEN \"AcademicYearStart\" AND 2200");

            migrationBuilder.AddForeignKey(
                name: "FK_official_curriculum_editions_study_catalog_exams_ExamCode",
                schema: "coaching",
                table: "official_curriculum_editions",
                column: "ExamCode",
                principalSchema: "coaching",
                principalTable: "study_catalog_exams",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_study_catalog_lessons_study_catalog_exams_ExamCode",
                schema: "coaching",
                table: "study_catalog_lessons",
                column: "ExamCode",
                principalSchema: "coaching",
                principalTable: "study_catalog_exams",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM coaching.official_curriculum_editions WHERE "ExamCode" <> 'LGS' OR "GradeNumber" <> 8)
                    THEN RAISE EXCEPTION 'New curriculum scopes exist; preserve them before reverting the migration.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_official_curriculum_editions_study_catalog_exams_ExamCode",
                schema: "coaching",
                table: "official_curriculum_editions");

            migrationBuilder.DropForeignKey(
                name: "FK_study_catalog_lessons_study_catalog_exams_ExamCode",
                schema: "coaching",
                table: "study_catalog_lessons");

            migrationBuilder.DropTable(
                name: "study_catalog_exams",
                schema: "coaching");

            migrationBuilder.DropIndex(
                name: "IX_study_catalog_lessons_ExamCode",
                schema: "coaching",
                table: "study_catalog_lessons");

            migrationBuilder.DropIndex(
                name: "IX_official_curriculum_editions_ExamCode",
                schema: "coaching",
                table: "official_curriculum_editions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_outcome_edition_scope",
                schema: "coaching",
                table: "official_curriculum_editions");

            migrationBuilder.AddCheckConstraint(
                name: "ck_outcome_edition_scope",
                schema: "coaching",
                table: "official_curriculum_editions",
                sql: "\"ExamCode\" = 'LGS' AND \"GradeNumber\" = 8 AND \"AcademicYearStart\" BETWEEN 2000 AND 2200 AND \"AcademicYearEnd\" BETWEEN \"AcademicYearStart\" AND 2200");
        }
    }
}
