using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class StudyCatalogOutcomes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "official_curriculum_editions",
                schema: "coaching",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ExamCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    GradeNumber = table.Column<int>(type: "integer", nullable: false),
                    SubjectName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    AcademicYearStart = table.Column<int>(type: "integer", nullable: false),
                    AcademicYearEnd = table.Column<int>(type: "integer", nullable: false),
                    SourceUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    DocumentSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ScopeEvidenceUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    PackageSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_official_curriculum_editions", x => x.Id);
                    table.CheckConstraint("ck_outcome_edition_scope", "\"ExamCode\" = 'LGS' AND \"GradeNumber\" = 8 AND \"AcademicYearStart\" BETWEEN 2000 AND 2200 AND \"AcademicYearEnd\" BETWEEN \"AcademicYearStart\" AND 2200");
                });

            migrationBuilder.CreateTable(
                name: "official_curriculum_topics",
                schema: "coaching",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EditionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    OutcomeCount = table.Column<int>(type: "integer", nullable: false),
                    LessonPeriods = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_official_curriculum_topics", x => x.Id);
                    table.UniqueConstraint("AK_official_curriculum_topics_Id_EditionId", x => new { x.Id, x.EditionId });
                    table.CheckConstraint("ck_official_topic_budget", "\"OutcomeCount\" BETWEEN 1 AND 500 AND \"LessonPeriods\" BETWEEN 1 AND 500");
                    table.ForeignKey(
                        name: "FK_official_curriculum_topics_official_curriculum_editions_Edi~",
                        column: x => x.EditionId,
                        principalSchema: "coaching",
                        principalTable: "official_curriculum_editions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "study_catalog_outcomes",
                schema: "coaching",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EditionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OfficialTopicId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Notes = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    SourcePage = table.Column<int>(type: "integer", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_study_catalog_outcomes", x => x.Id);
                    table.CheckConstraint("ck_outcome_source", "\"SourcePage\" > 0 AND \"DisplayOrder\" > 0");
                    table.ForeignKey(
                        name: "FK_study_catalog_outcomes_official_curriculum_topics_OfficialT~",
                        columns: x => new { x.OfficialTopicId, x.EditionId },
                        principalSchema: "coaching",
                        principalTable: "official_curriculum_topics",
                        principalColumns: new[] { "Id", "EditionId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "outcome_duration_estimates",
                schema: "coaching",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OutcomeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    LearningMinutes = table.Column<int>(type: "integer", nullable: false),
                    PracticeMinutes = table.Column<int>(type: "integer", nullable: false),
                    CheckMinutes = table.Column<int>(type: "integer", nullable: false),
                    Method = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Confidence = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Evidence = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ReviewedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outcome_duration_estimates", x => x.Id);
                    table.UniqueConstraint("AK_outcome_duration_estimates_Id_OutcomeId", x => new { x.Id, x.OutcomeId });
                    table.CheckConstraint("ck_outcome_minutes", "\"LearningMinutes\" >= 0 AND \"PracticeMinutes\" >= 0 AND \"CheckMinutes\" >= 0 AND (\"LearningMinutes\"::bigint + \"PracticeMinutes\" + \"CheckMinutes\") BETWEEN 1 AND 1440 AND \"Number\" > 0");
                    table.CheckConstraint("ck_outcome_review", "\"Method\" IN ('OfficialBudgetDraft','ReviewedStudy') AND \"Confidence\" IN ('Low','Medium','High') AND (\"Method\" <> 'ReviewedStudy' OR (\"ReviewedBy\" IS NOT NULL AND \"ReviewedAt\" IS NOT NULL)) AND (NOT \"IsPublished\" OR \"Method\" = 'ReviewedStudy')");
                    table.ForeignKey(
                        name: "FK_outcome_duration_estimates_study_catalog_outcomes_OutcomeId",
                        column: x => x.OutcomeId,
                        principalSchema: "coaching",
                        principalTable: "study_catalog_outcomes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "study_catalog_outcome_sources",
                schema: "coaching",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OutcomeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SourceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_study_catalog_outcome_sources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_study_catalog_outcome_sources_study_catalog_outcomes_Outcom~",
                        column: x => x.OutcomeId,
                        principalSchema: "coaching",
                        principalTable: "study_catalog_outcomes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "study_catalog_topic_outcomes",
                schema: "coaching",
                columns: table => new
                {
                    TopicId = table.Column<Guid>(type: "uuid", nullable: false),
                    OutcomeId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsApproved = table.Column<bool>(type: "boolean", nullable: false),
                    Evidence = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ReviewedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_study_catalog_topic_outcomes", x => new { x.TopicId, x.OutcomeId });
                    table.ForeignKey(
                        name: "FK_study_catalog_topic_outcomes_study_catalog_outcomes_Outcome~",
                        column: x => x.OutcomeId,
                        principalSchema: "coaching",
                        principalTable: "study_catalog_outcomes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_study_catalog_topic_outcomes_study_catalog_topics_TopicId",
                        column: x => x.TopicId,
                        principalSchema: "coaching",
                        principalTable: "study_catalog_topics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "study_plan_task_outcomes",
                schema: "coaching",
                columns: table => new
                {
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    OutcomeId = table.Column<Guid>(type: "uuid", nullable: false),
                    EstimateId = table.Column<Guid>(type: "uuid", nullable: false),
                    AllocatedMinutes = table.Column<int>(type: "integer", nullable: false),
                    Origin = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_study_plan_task_outcomes", x => new { x.TaskId, x.OutcomeId });
                    table.CheckConstraint("ck_task_outcome_minutes", "\"AllocatedMinutes\" BETWEEN 1 AND 1440 AND \"Origin\" IN ('Catalog','StudentOverride')");
                    table.ForeignKey(
                        name: "FK_study_plan_task_outcomes_outcome_duration_estimates_Estimat~",
                        columns: x => new { x.EstimateId, x.OutcomeId },
                        principalSchema: "coaching",
                        principalTable: "outcome_duration_estimates",
                        principalColumns: new[] { "Id", "OutcomeId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_study_plan_task_outcomes_study_plan_tasks_TaskId",
                        column: x => x.TaskId,
                        principalSchema: "coaching",
                        principalTable: "study_plan_tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_official_curriculum_editions_Code",
                schema: "coaching",
                table: "official_curriculum_editions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_official_curriculum_topics_EditionId_Code",
                schema: "coaching",
                table: "official_curriculum_topics",
                columns: new[] { "EditionId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_outcome_duration_estimates_OutcomeId_Number",
                schema: "coaching",
                table: "outcome_duration_estimates",
                columns: new[] { "OutcomeId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_study_catalog_outcome_sources_OutcomeId",
                schema: "coaching",
                table: "study_catalog_outcome_sources",
                column: "OutcomeId");

            migrationBuilder.CreateIndex(
                name: "IX_study_catalog_outcome_sources_Source_SourceId",
                schema: "coaching",
                table: "study_catalog_outcome_sources",
                columns: new[] { "Source", "SourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_study_catalog_outcomes_EditionId_Code",
                schema: "coaching",
                table: "study_catalog_outcomes",
                columns: new[] { "EditionId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_study_catalog_outcomes_OfficialTopicId_EditionId",
                schema: "coaching",
                table: "study_catalog_outcomes",
                columns: new[] { "OfficialTopicId", "EditionId" });

            migrationBuilder.CreateIndex(
                name: "IX_study_catalog_topic_outcomes_OutcomeId",
                schema: "coaching",
                table: "study_catalog_topic_outcomes",
                column: "OutcomeId");

            migrationBuilder.CreateIndex(
                name: "IX_study_plan_task_outcomes_EstimateId_OutcomeId",
                schema: "coaching",
                table: "study_plan_task_outcomes",
                columns: new[] { "EstimateId", "OutcomeId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "study_catalog_outcome_sources",
                schema: "coaching");

            migrationBuilder.DropTable(
                name: "study_catalog_topic_outcomes",
                schema: "coaching");

            migrationBuilder.DropTable(
                name: "study_plan_task_outcomes",
                schema: "coaching");

            migrationBuilder.DropTable(
                name: "outcome_duration_estimates",
                schema: "coaching");

            migrationBuilder.DropTable(
                name: "study_catalog_outcomes",
                schema: "coaching");

            migrationBuilder.DropTable(
                name: "official_curriculum_topics",
                schema: "coaching");

            migrationBuilder.DropTable(
                name: "official_curriculum_editions",
                schema: "coaching");
        }
    }
}
