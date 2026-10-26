using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class YksExamPreparationCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_outcome_source",
                schema: "coaching",
                table: "study_catalog_outcomes");

            migrationBuilder.DropCheckConstraint(
                name: "ck_official_topic_budget",
                schema: "coaching",
                table: "official_curriculum_topics");

            migrationBuilder.AlterColumn<int>(
                name: "SourcePage",
                schema: "coaching",
                table: "study_catalog_outcomes",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<string>(
                name: "SourceKind",
                schema: "coaching",
                table: "study_catalog_outcomes",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Official");

            migrationBuilder.AddColumn<string>(
                name: "SourceUrl",
                schema: "coaching",
                table: "study_catalog_outcomes",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "LessonPeriods",
                schema: "coaching",
                table: "official_curriculum_topics",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.CreateTable(
                name: "official_curriculum_lesson_bindings",
                schema: "coaching",
                columns: table => new
                {
                    EditionId = table.Column<Guid>(type: "uuid", nullable: false),
                    LessonId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_official_curriculum_lesson_bindings", x => new { x.EditionId, x.LessonId });
                    table.ForeignKey(
                        name: "FK_official_curriculum_lesson_bindings_official_curriculum_edi~",
                        column: x => x.EditionId,
                        principalSchema: "coaching",
                        principalTable: "official_curriculum_editions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_official_curriculum_lesson_bindings_study_catalog_lessons_L~",
                        column: x => x.LessonId,
                        principalSchema: "coaching",
                        principalTable: "study_catalog_lessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "study_catalog_yks_scopes",
                schema: "coaching",
                columns: table => new
                {
                    TopicId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScoreType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    TestCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ExamCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ForeignLanguage = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Evidence = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ExcludedOutcomeIds = table.Column<Guid[]>(type: "uuid[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_study_catalog_yks_scopes", x => new { x.TopicId, x.ScoreType, x.TestCode });
                    table.CheckConstraint("ck_yks_scope", "\"ExamCode\" IN ('TYT','AYT','YDT') AND \"ScoreType\" IN ('TYT','SAY','EA','SOZ','DIL') AND (\"ExamCode\" <> 'AYT' OR \"ScoreType\" IN ('SAY','EA','SOZ')) AND ((\"ExamCode\" = 'YDT' AND \"ScoreType\" = 'DIL' AND \"ForeignLanguage\" IS NOT NULL AND \"ForeignLanguage\" = 'English') OR (\"ExamCode\" <> 'YDT' AND \"ForeignLanguage\" IS NULL))");
                    table.ForeignKey(
                        name: "FK_study_catalog_yks_scopes_study_catalog_exams_ExamCode",
                        column: x => x.ExamCode,
                        principalSchema: "coaching",
                        principalTable: "study_catalog_exams",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_study_catalog_yks_scopes_study_catalog_topics_TopicId",
                        column: x => x.TopicId,
                        principalSchema: "coaching",
                        principalTable: "study_catalog_topics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_outcome_source",
                schema: "coaching",
                table: "study_catalog_outcomes",
                sql: "(\"SourcePage\" IS NULL OR \"SourcePage\" > 0) AND \"DisplayOrder\" > 0 AND \"SourceKind\" IN ('Official','ExamSkill') AND (\"SourceKind\" <> 'Official' OR \"SourcePage\" IS NOT NULL) AND (\"SourceKind\" <> 'ExamSkill' OR \"SourceUrl\" IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_official_topic_budget",
                schema: "coaching",
                table: "official_curriculum_topics",
                sql: "\"OutcomeCount\" BETWEEN 1 AND 500 AND (\"LessonPeriods\" IS NULL OR \"LessonPeriods\" BETWEEN 1 AND 500)");

            migrationBuilder.CreateIndex(
                name: "IX_official_curriculum_lesson_bindings_LessonId",
                schema: "coaching",
                table: "official_curriculum_lesson_bindings",
                column: "LessonId");

            migrationBuilder.CreateIndex(
                name: "IX_study_catalog_yks_scopes_ExamCode",
                schema: "coaching",
                table: "study_catalog_yks_scopes",
                column: "ExamCode");

            migrationBuilder.CreateIndex(
                name: "IX_study_catalog_yks_scopes_ScoreType_ExamCode_ForeignLanguage",
                schema: "coaching",
                table: "study_catalog_yks_scopes",
                columns: new[] { "ScoreType", "ExamCode", "ForeignLanguage" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "official_curriculum_lesson_bindings",
                schema: "coaching");

            migrationBuilder.DropTable(
                name: "study_catalog_yks_scopes",
                schema: "coaching");

            migrationBuilder.DropCheckConstraint(
                name: "ck_outcome_source",
                schema: "coaching",
                table: "study_catalog_outcomes");

            migrationBuilder.DropCheckConstraint(
                name: "ck_official_topic_budget",
                schema: "coaching",
                table: "official_curriculum_topics");

            migrationBuilder.DropColumn(
                name: "SourceKind",
                schema: "coaching",
                table: "study_catalog_outcomes");

            migrationBuilder.DropColumn(
                name: "SourceUrl",
                schema: "coaching",
                table: "study_catalog_outcomes");

            migrationBuilder.AlterColumn<int>(
                name: "SourcePage",
                schema: "coaching",
                table: "study_catalog_outcomes",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "LessonPeriods",
                schema: "coaching",
                table: "official_curriculum_topics",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_outcome_source",
                schema: "coaching",
                table: "study_catalog_outcomes",
                sql: "\"SourcePage\" > 0 AND \"DisplayOrder\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_official_topic_budget",
                schema: "coaching",
                table: "official_curriculum_topics",
                sql: "\"OutcomeCount\" BETWEEN 1 AND 500 AND \"LessonPeriods\" BETWEEN 1 AND 500");
        }
    }
}
