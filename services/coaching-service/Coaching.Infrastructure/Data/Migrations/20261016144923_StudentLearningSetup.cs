using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class StudentLearningSetup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CurriculumVersionId",
                schema: "coaching",
                table: "study_catalog_lessons",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "curriculum_versions",
                schema: "coaching",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    VersionLabel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ValidFromYear = table.Column<int>(type: "integer", nullable: false),
                    ValidToYear = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    SourceUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_curriculum_versions", x => x.Id);
                    table.CheckConstraint("ck_curriculum_years", "\"ValidFromYear\" BETWEEN 2000 AND 2200 AND \"ValidToYear\" BETWEEN \"ValidFromYear\" AND 2200");
                });

            migrationBuilder.CreateTable(
                name: "student_learning_contexts",
                schema: "coaching",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcademicYearStart = table.Column<int>(type: "integer", nullable: false),
                    Purpose = table.Column<int>(type: "integer", nullable: false),
                    TargetExamYear = table.Column<int>(type: "integer", nullable: true),
                    PrimaryScoreType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ForeignLanguage = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    CurriculumVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    EducationStatus = table.Column<int>(type: "integer", nullable: false),
                    GradeLevel = table.Column<int>(type: "integer", nullable: true),
                    LastCompletedStep = table.Column<int>(type: "integer", nullable: false),
                    SetupVersion = table.Column<int>(type: "integer", nullable: false),
                    StartingStatusReviewed = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_learning_contexts", x => x.Id);
                    table.CheckConstraint("ck_learning_purpose", "\"Purpose\" BETWEEN 0 AND 4");
                    table.CheckConstraint("ck_learning_year", "\"AcademicYearStart\" BETWEEN 2000 AND 2200");
                    table.ForeignKey(
                        name: "FK_student_learning_contexts_curriculum_versions_CurriculumVer~",
                        column: x => x.CurriculumVersionId,
                        principalSchema: "coaching",
                        principalTable: "curriculum_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_student_learning_contexts_student_profiles_StudentUserId",
                        column: x => x.StudentUserId,
                        principalSchema: "coaching",
                        principalTable: "student_profiles",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "student_course_preferences",
                schema: "coaching",
                columns: table => new
                {
                    ContextId = table.Column<Guid>(type: "uuid", nullable: false),
                    LessonId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsIncluded = table.Column<bool>(type: "boolean", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    SelfReportedDifficulty = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_course_preferences", x => new { x.ContextId, x.LessonId });
                    table.CheckConstraint("ck_course_priority", "\"Priority\" BETWEEN 1 AND 5 AND (\"SelfReportedDifficulty\" IS NULL OR \"SelfReportedDifficulty\" BETWEEN 1 AND 5)");
                    table.ForeignKey(
                        name: "FK_student_course_preferences_student_learning_contexts_Contex~",
                        column: x => x.ContextId,
                        principalSchema: "coaching",
                        principalTable: "student_learning_contexts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_student_course_preferences_study_catalog_lessons_LessonId",
                        column: x => x.LessonId,
                        principalSchema: "coaching",
                        principalTable: "study_catalog_lessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "student_topic_states",
                schema: "coaching",
                columns: table => new
                {
                    ContextId = table.Column<Guid>(type: "uuid", nullable: false),
                    TopicId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeclaredStatus = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_topic_states", x => new { x.ContextId, x.TopicId });
                    table.CheckConstraint("ck_topic_declaration", "\"DeclaredStatus\" BETWEEN 0 AND 2");
                    table.ForeignKey(
                        name: "FK_student_topic_states_student_learning_contexts_ContextId",
                        column: x => x.ContextId,
                        principalSchema: "coaching",
                        principalTable: "student_learning_contexts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_student_topic_states_study_catalog_topics_TopicId",
                        column: x => x.TopicId,
                        principalSchema: "coaching",
                        principalTable: "study_catalog_topics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_study_catalog_lessons_CurriculumVersionId",
                schema: "coaching",
                table: "study_catalog_lessons",
                column: "CurriculumVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_curriculum_versions_Code",
                schema: "coaching",
                table: "curriculum_versions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_student_course_preferences_LessonId",
                schema: "coaching",
                table: "student_course_preferences",
                column: "LessonId");

            migrationBuilder.CreateIndex(
                name: "IX_student_learning_contexts_CurriculumVersionId",
                schema: "coaching",
                table: "student_learning_contexts",
                column: "CurriculumVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_student_learning_contexts_StudentUserId",
                schema: "coaching",
                table: "student_learning_contexts",
                column: "StudentUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_student_topic_states_TopicId",
                schema: "coaching",
                table: "student_topic_states",
                column: "TopicId");

            migrationBuilder.AddForeignKey(
                name: "FK_study_catalog_lessons_curriculum_versions_CurriculumVersion~",
                schema: "coaching",
                table: "study_catalog_lessons",
                column: "CurriculumVersionId",
                principalSchema: "coaching",
                principalTable: "curriculum_versions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_study_catalog_lessons_curriculum_versions_CurriculumVersion~",
                schema: "coaching",
                table: "study_catalog_lessons");

            migrationBuilder.DropTable(
                name: "student_course_preferences",
                schema: "coaching");

            migrationBuilder.DropTable(
                name: "student_topic_states",
                schema: "coaching");

            migrationBuilder.DropTable(
                name: "student_learning_contexts",
                schema: "coaching");

            migrationBuilder.DropTable(
                name: "curriculum_versions",
                schema: "coaching");

            migrationBuilder.DropIndex(
                name: "IX_study_catalog_lessons_CurriculumVersionId",
                schema: "coaching",
                table: "study_catalog_lessons");

            migrationBuilder.DropColumn(
                name: "CurriculumVersionId",
                schema: "coaching",
                table: "study_catalog_lessons");
        }
    }
}
